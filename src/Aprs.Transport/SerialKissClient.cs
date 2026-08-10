using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aprs.Transport;

public sealed class SerialKissClient : ISerialKissClient
{
    private readonly SerialKissConfiguration configuration;
    public  SerialKissConfiguration Configuration => configuration;
    private readonly ISerialPortConnectionFactory connectionFactory;
    private readonly IAx25AprsPayloadDecoder payloadDecoder;
    private readonly Channel<KissFrame> receivedFrames = Channel.CreateBounded<KissFrame>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<TcpKissRawPacketReceivedEventArgs> receivedPackets = Channel.CreateBounded<TcpKissRawPacketReceivedEventArgs>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;

    // Guards the mutable connection reference and the State/LastError fields, which are otherwise
    // read by the send path while the receive loop reassigns them during a reconnect. Never held
    // across an await: callers snapshot the connection under the lock, then do I/O on the snapshot.
    private readonly object sync = new();
    private ISerialPortConnection? connection;
    private SerialKissConnectionState state = SerialKissConnectionState.Disconnected;
    private Exception? lastError;

    public SerialKissClient(SerialKissConfiguration configuration, ISerialPortConnectionFactory connectionFactory)
        : this(configuration, connectionFactory, Ax25AprsPayloadDecoder.Default)
    {
    }

    public SerialKissClient(
        SerialKissConfiguration configuration,
        ISerialPortConnectionFactory connectionFactory,
        IAx25AprsPayloadDecoder payloadDecoder)
    {
        this.configuration = configuration;
        this.connectionFactory = connectionFactory;
        this.payloadDecoder = payloadDecoder;
    }

    public event EventHandler<KissFrameReceivedEventArgs>? FrameReceived;

    public event EventHandler<TcpKissRawPacketReceivedEventArgs>? RawPacketReceived;

    public SerialKissConnectionState State { get { lock (sync) { return state; } } }

    public Exception? LastError { get { lock (sync) { return lastError; } } }

    private void SetState(SerialKissConnectionState value) { lock (sync) { state = value; } }

    private void Fault(Exception exception)
    {
        lock (sync) { lastError = exception; state = SerialKissConnectionState.Faulted; }
    }

    private void SetConnection(ISerialPortConnection? value) { lock (sync) { connection = value; } }

    private (ISerialPortConnection? Connection, SerialKissConnectionState State) Snapshot()
    {
        lock (sync) { return (connection, state); }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            SetState(SerialKissConnectionState.Disconnected);
            return;
        }

        if (State is SerialKissConnectionState.Connected or SerialKissConnectionState.Connecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (sync) { state = SerialKissConnectionState.Connecting; lastError = null; }

        try
        {
            var opened = connectionFactory.Create(configuration);
            await opened.OpenAsync(connectionCancellation.Token).ConfigureAwait(false);
            SetConnection(opened);
            SetState(SerialKissConnectionState.Connected);
            if (configuration.ReceiveEnabled)
            {
                receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        connectionCancellation?.Cancel();

        ISerialPortConnection? toClose;
        lock (sync) { toClose = connection; }
        if (toClose is not null)
        {
            await toClose.CloseAsync(cancellationToken).ConfigureAwait(false);
        }

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        SetState(SerialKissConnectionState.Disconnected);
    }

    public async IAsyncEnumerable<KissFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await receivedFrames.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (receivedFrames.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }

    public async IAsyncEnumerable<TcpKissRawPacketReceivedEventArgs> ReadPacketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await receivedPackets.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (receivedPackets.Reader.TryRead(out var packet))
            {
                yield return packet;
            }
        }
    }

    public async Task<SerialKissTransmitResult> SendFrameAsync(
        int portNumber,
        KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload,
        bool transmitConfirmed,
        bool rfSafetyEnabled,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        // Consistent snapshot so a concurrent reconnect cannot swap the connection mid-write and so
        // validation and I/O see the same connection object.
        var (active, stateAtRequest) = Snapshot();
        var failureReason = ValidateTransmitRequest(portNumber, commandType, ax25Payload, transmitConfirmed, rfSafetyEnabled, stateAtRequest, active);
        if (failureReason is not null)
        {
            return SerialKissTransmitResult.Failed(timestamp, stateAtRequest, failureReason);
        }

        var encoded = KissFrameCodec.Encode(portNumber, commandType, ax25Payload);
        var frame = KissFrameCodec.Decode(encoded, timestamp, configuration.SourceName, payloadDecoder);

        try
        {
            await active!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            return SerialKissTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception);
            return SerialKissTransmitResult.Failed(timestamp, stateAtRequest, exception.Message, frame);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        connectionCancellation?.Dispose();

        ISerialPortConnection? toDispose;
        lock (sync) { toDispose = connection; connection = null; }
        if (toDispose is not null)
        {
            await toDispose.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var readBuffer = new byte[4096];
        var pending = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ISerialPortConnection? active;
                lock (sync) { active = connection; }
                if (active is null) break;

                var bytesRead = await active.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        SetState(SerialKissConnectionState.Disconnected);
                        break;
                    }

                    SetState(SerialKissConnectionState.Reconnecting);
                    await active.CloseAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                    var reopened = connectionFactory.Create(configuration);
                    await reopened.OpenAsync(cancellationToken).ConfigureAwait(false);
                    SetConnection(reopened);
                    SetState(SerialKissConnectionState.Connected);
                    continue;
                }

                pending.AddRange(readBuffer.Take(bytesRead));
                var lastCompleteEnd = KissFrameCodec.FindLastCompleteFrameEnd(pending);
                if (lastCompleteEnd < 0)
                {
                    continue;
                }

                var completeBytes = pending.Take(lastCompleteEnd + 1).ToArray();
                pending.RemoveRange(0, lastCompleteEnd + 1);

                foreach (var frame in KissFrameCodec.DecodeMany(completeBytes, DateTimeOffset.UtcNow, configuration.SourceName, payloadDecoder))
                {
                    PublishFrame(frame);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            Fault(exception);
        }
    }

    private void PublishFrame(KissFrame frame)
    {
        receivedFrames.Writer.TryWrite(frame);
        FrameReceived?.Invoke(this, new KissFrameReceivedEventArgs(frame));

        if (frame.DecodedAprsPacketText is null)
        {
            return;
        }

        var packet = new TcpKissRawPacketReceivedEventArgs(frame.DecodedAprsPacketText, frame.TimestampUtc, frame);
        receivedPackets.Writer.TryWrite(packet);
        RawPacketReceived?.Invoke(this, packet);
    }

    private string? ValidateTransmitRequest(
        int portNumber,
        KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload,
        bool transmitConfirmed,
        bool rfSafetyEnabled,
        SerialKissConnectionState stateAtRequest,
        ISerialPortConnection? activeConnection)
    {
        if (!configuration.TransmitEnabled)
        {
            return "Serial KISS transmit is disabled.";
        }

        if (!rfSafetyEnabled)
        {
            return "RF transmit safety settings are not enabled.";
        }

        if (!transmitConfirmed)
        {
            return "Serial KISS transmit confirmation is required.";
        }

        if (stateAtRequest != SerialKissConnectionState.Connected || activeConnection is null)
        {
            return "Serial KISS client is not connected.";
        }

        if (!activeConnection.IsOpen)
        {
            return "Serial port is not open.";
        }

        if (portNumber is < 0 or > 15)
        {
            return "KISS port number must be between 0 and 15.";
        }

        if (commandType == KissCommandType.Unknown)
        {
            return "KISS command type is unknown.";
        }

        if (ax25Payload.Count == 0)
        {
            return "KISS payload cannot be empty.";
        }

        return null;
    }

    private static void ValidateConfiguration(SerialKissConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.PortName))
        {
            throw new ArgumentException("Serial KISS port name is required.", nameof(configuration));
        }

        if (configuration.BaudRate <= 0)
        {
            throw new ArgumentException("Serial KISS baud rate must be positive.", nameof(configuration));
        }

        if (configuration.DataBits is < 5 or > 8)
        {
            throw new ArgumentException("Serial KISS data bits must be between 5 and 8.", nameof(configuration));
        }
    }
}
