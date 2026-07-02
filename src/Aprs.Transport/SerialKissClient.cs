using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aprs.Transport;

public sealed class SerialKissClient : ISerialKissClient
{
    private readonly SerialKissConfiguration configuration;
    public  SerialKissConfiguration Configuration => configuration;
    private readonly ISerialPortConnectionFactory connectionFactory;
    private readonly IAx25AprsPayloadDecoder payloadDecoder;
    private readonly Channel<KissFrame> receivedFrames = Channel.CreateUnbounded<KissFrame>();
    private readonly Channel<TcpKissRawPacketReceivedEventArgs> receivedPackets = Channel.CreateUnbounded<TcpKissRawPacketReceivedEventArgs>();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;
    private ISerialPortConnection? connection;

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

    public SerialKissConnectionState State { get; private set; } = SerialKissConnectionState.Disconnected;

    public Exception? LastError { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            State = SerialKissConnectionState.Disconnected;
            return;
        }

        if (State is SerialKissConnectionState.Connected or SerialKissConnectionState.Connecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        State = SerialKissConnectionState.Connecting;
        LastError = null;

        try
        {
            connection = connectionFactory.Create(configuration);
            await connection.OpenAsync(connectionCancellation.Token).ConfigureAwait(false);
            State = SerialKissConnectionState.Connected;
            if (configuration.ReceiveEnabled)
            {
                receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = SerialKissConnectionState.Faulted;
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        connectionCancellation?.Cancel();

        if (connection is not null)
        {
            await connection.CloseAsync(cancellationToken).ConfigureAwait(false);
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

        State = SerialKissConnectionState.Disconnected;
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
        var stateAtRequest = State;
        var failureReason = ValidateTransmitRequest(portNumber, commandType, ax25Payload, transmitConfirmed, rfSafetyEnabled, stateAtRequest);
        if (failureReason is not null)
        {
            return SerialKissTransmitResult.Failed(timestamp, stateAtRequest, failureReason);
        }

        var encoded = KissFrameCodec.Encode(portNumber, commandType, ax25Payload);
        var frame = KissFrameCodec.Decode(encoded, timestamp, configuration.SourceName, payloadDecoder);

        try
        {
            await connection!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            return SerialKissTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = SerialKissConnectionState.Faulted;
            return SerialKissTransmitResult.Failed(timestamp, stateAtRequest, exception.Message, frame);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        connectionCancellation?.Dispose();
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var readBuffer = new byte[4096];
        var pending = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && connection is not null)
            {
                var bytesRead = await connection.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        State = SerialKissConnectionState.Disconnected;
                        break;
                    }

                    State = SerialKissConnectionState.Reconnecting;
                    await connection.CloseAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                    connection = connectionFactory.Create(configuration);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    State = SerialKissConnectionState.Connected;
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
            LastError = exception;
            State = SerialKissConnectionState.Faulted;
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
        SerialKissConnectionState stateAtRequest)
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

        if (stateAtRequest != SerialKissConnectionState.Connected || connection is null)
        {
            return "Serial KISS client is not connected.";
        }

        if (!connection.IsOpen)
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
