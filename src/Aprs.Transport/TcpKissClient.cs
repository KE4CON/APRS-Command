using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Aprs.Transport;

public sealed class TcpKissClient : ITcpKissClient
{
    private readonly TcpKissConfiguration configuration;
    public  TcpKissConfiguration Configuration => configuration;
    private readonly Func<TcpKissConfiguration, CancellationToken, Task<Stream>> streamFactory;
    private readonly IAx25AprsPayloadDecoder payloadDecoder;
    private readonly Channel<KissFrame> receivedFrames = Channel.CreateBounded<KissFrame>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<TcpKissRawPacketReceivedEventArgs> receivedPackets = Channel.CreateBounded<TcpKissRawPacketReceivedEventArgs>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;

    // Guards the mutable connection reference and the State/LastError fields, which are otherwise
    // read by the send path while the receive loop reassigns them during a reconnect. Never held
    // across an await: callers snapshot the stream under the lock, then do I/O on the snapshot.
    private readonly object sync = new();
    private Stream? stream;
    private TcpKissConnectionState state = TcpKissConnectionState.Disconnected;
    private Exception? lastError;

    public TcpKissClient(TcpKissConfiguration configuration)
        : this(configuration, CreateTcpStreamAsync, Ax25AprsPayloadDecoder.Default)
    {
    }

    public TcpKissClient(
        TcpKissConfiguration configuration,
        Func<TcpKissConfiguration, CancellationToken, Task<Stream>> streamFactory)
        : this(configuration, streamFactory, Ax25AprsPayloadDecoder.Default)
    {
    }

    public TcpKissClient(
        TcpKissConfiguration configuration,
        Func<TcpKissConfiguration, CancellationToken, Task<Stream>> streamFactory,
        IAx25AprsPayloadDecoder payloadDecoder)
    {
        this.configuration = configuration;
        this.streamFactory = streamFactory;
        this.payloadDecoder = payloadDecoder;
    }

    public event EventHandler<KissFrameReceivedEventArgs>? FrameReceived;

    public event EventHandler<TcpKissRawPacketReceivedEventArgs>? RawPacketReceived;

    public TcpKissConnectionState State { get { lock (sync) { return state; } } }

    public Exception? LastError { get { lock (sync) { return lastError; } } }

    private void SetState(TcpKissConnectionState value) { lock (sync) { state = value; } }

    private void Fault(Exception exception)
    {
        lock (sync) { lastError = exception; state = TcpKissConnectionState.Faulted; }
    }

    private void SetStream(Stream? value) { lock (sync) { stream = value; } }

    private (Stream? Stream, TcpKissConnectionState State) Snapshot()
    {
        lock (sync) { return (stream, state); }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            SetState(TcpKissConnectionState.Disconnected);
            return;
        }

        if (State is TcpKissConnectionState.Connected or TcpKissConnectionState.Connecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (sync) { state = TcpKissConnectionState.Connecting; lastError = null; }

        try
        {
            var opened = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            SetStream(opened);
            SetState(TcpKissConnectionState.Connected);
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

        Stream? toDispose;
        lock (sync) { toDispose = stream; stream = null; }
        if (toDispose is not null)
        {
            await toDispose.DisposeAsync().ConfigureAwait(false);
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

        SetState(TcpKissConnectionState.Disconnected);
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

    public async Task<TcpKissTransmitResult> SendFrameAsync(
        int portNumber,
        KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload,
        bool transmitConfirmed,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        // Take a consistent snapshot of the stream and state so a concurrent reconnect cannot swap
        // the stream between the write and the flush, and so validation and I/O see the same stream.
        var (active, stateAtRequest) = Snapshot();
        var failureReason = ValidateTransmitRequest(portNumber, commandType, ax25Payload, transmitConfirmed, stateAtRequest, active);
        if (failureReason is not null)
        {
            return TcpKissTransmitResult.Failed(timestamp, stateAtRequest, failureReason);
        }

        var encoded = KissFrameCodec.Encode(portNumber, commandType, ax25Payload);
        var frame = KissFrameCodec.Decode(encoded, timestamp, configuration.SourceName, payloadDecoder);

        try
        {
            await active!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            await active.FlushAsync(cancellationToken).ConfigureAwait(false);
            return TcpKissTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception);
            return TcpKissTransmitResult.Failed(timestamp, stateAtRequest, exception.Message, frame);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        connectionCancellation?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var readBuffer = new byte[4096];
        var pending = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Stream? active;
                lock (sync) { active = stream; }
                if (active is null) break;

                var bytesRead = await active.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        SetState(TcpKissConnectionState.Disconnected);
                        break;
                    }

                    SetState(TcpKissConnectionState.Reconnecting);
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);

                    // Dispose the closed stream (the one we were reading from) before replacing it —
                    // otherwise each reconnect leaks the previous NetworkStream/socket.
                    try { await active.DisposeAsync().ConfigureAwait(false); }
                    catch { /* best-effort cleanup of the dead stream */ }

                    var reopened = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                    SetStream(reopened);
                    SetState(TcpKissConnectionState.Connected);
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
        TcpKissConnectionState stateAtRequest,
        Stream? activeStream)
    {
        if (!configuration.TransmitEnabled)
        {
            return "TCP KISS transmit is disabled.";
        }

        if (!transmitConfirmed)
        {
            return "TCP KISS transmit confirmation is required.";
        }

        if (stateAtRequest != TcpKissConnectionState.Connected || activeStream is null)
        {
            return "TCP KISS client is not connected.";
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

    private static void ValidateConfiguration(TcpKissConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Host))
        {
            throw new ArgumentException("TCP KISS host is required.", nameof(configuration));
        }

        if (configuration.Port is < 1 or > 65535)
        {
            throw new ArgumentException("TCP KISS port must be between 1 and 65535.", nameof(configuration));
        }
    }

    private static async Task<Stream> CreateTcpStreamAsync(TcpKissConfiguration configuration, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(configuration.Host, configuration.Port, cancellationToken).ConfigureAwait(false);
        return tcpClient.GetStream();
    }
}
