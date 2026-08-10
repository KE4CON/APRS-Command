using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Aprs.Core;

namespace Aprs.Transport;

public sealed class AgwpeClient : IAgwpeClient
{
    private readonly AgwpeConfiguration configuration;
    private readonly Func<AgwpeConfiguration, CancellationToken, Task<Stream>> streamFactory;
    private readonly AgwpeFrameCodec codec;
    private readonly AprsParser aprsParser = new();
    private readonly Channel<AgwpeFrame> receivedFrames = Channel.CreateBounded<AgwpeFrame>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<AgwpeRawPacketReceivedEventArgs> receivedPackets = Channel.CreateBounded<AgwpeRawPacketReceivedEventArgs>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    // Guards stream/state/lastError. The receive loop runs on a background thread while ConnectAsync,
    // DisconnectAsync and SendPacketAsync touch the same fields — AGWPE was missed by the thread-safety
    // pass the other transport clients got (audit H2). I/O is never done while holding the lock: the
    // stream is snapshotted under the lock, then read/written on the local copy.
    private readonly object sync = new();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;
    private Stream? stream;
    private AgwpeConnectionState state = AgwpeConnectionState.Disconnected;
    private Exception? lastError;

    public AgwpeClient(AgwpeConfiguration configuration)
        : this(configuration, CreateTcpStreamAsync, new AgwpeFrameCodec())
    {
    }

    public AgwpeClient(AgwpeConfiguration configuration, Func<AgwpeConfiguration, CancellationToken, Task<Stream>> streamFactory)
        : this(configuration, streamFactory, new AgwpeFrameCodec())
    {
    }

    public AgwpeClient(
        AgwpeConfiguration configuration,
        Func<AgwpeConfiguration, CancellationToken, Task<Stream>> streamFactory,
        AgwpeFrameCodec codec)
    {
        this.configuration = configuration;
        this.streamFactory = streamFactory;
        this.codec = codec;
    }

    public event EventHandler<AgwpeFrameReceivedEventArgs>? FrameReceived;

    public event EventHandler<AgwpeRawPacketReceivedEventArgs>? RawPacketReceived;

    /// <summary>
    /// Optional global transmit-inhibit gate. When set and inhibited (for example exercise mode),
    /// every <see cref="SendPacketAsync"/> call is blocked before any bytes reach the socket — so a
    /// caller that bypasses the beacon wrapper still cannot key up during a drill (audit Safety M/H).
    /// </summary>
    public ITransmitInhibitGate? InhibitGate { get; set; }

    public AgwpeConnectionState State { get { lock (sync) return state; } }

    public Exception? LastError { get { lock (sync) return lastError; } }

    private void SetState(AgwpeConnectionState newState) { lock (sync) state = newState; }

    private void Fault(Exception exception) { lock (sync) { lastError = exception; state = AgwpeConnectionState.Faulted; } }

    private Stream? SnapshotStream() { lock (sync) return stream; }

    private void SetStream(Stream? newStream) { lock (sync) stream = newStream; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            SetState(AgwpeConnectionState.Disconnected);
            return;
        }

        // Reconnecting is included so a Connect call during an in-progress auto-reconnect does not
        // start a second receive loop racing the first over the shared stream/state (transport M5).
        if (State is AgwpeConnectionState.Connected or AgwpeConnectionState.Connecting
            or AgwpeConnectionState.Reconnecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        SetState(AgwpeConnectionState.Connecting);
        lock (sync) lastError = null;

        try
        {
            var opened = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            SetStream(opened);
            SetState(AgwpeConnectionState.Connected);
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

        var current = SnapshotStream();
        if (current is not null)
        {
            await current.DisposeAsync().ConfigureAwait(false);
            SetStream(null);
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

        SetState(AgwpeConnectionState.Disconnected);
    }

    public async IAsyncEnumerable<AgwpeFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await receivedFrames.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (receivedFrames.Reader.TryRead(out var frame))
            {
                yield return frame;
            }
        }
    }

    public async IAsyncEnumerable<AgwpeRawPacketReceivedEventArgs> ReadPacketsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await receivedPackets.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (receivedPackets.Reader.TryRead(out var packet))
            {
                yield return packet;
            }
        }
    }

    public async Task<AgwpeTransmitResult> SendPacketAsync(
        string rawPacketLine,
        bool transmitConfirmed,
        bool rfSafetyEnabled,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var stateAtRequest = State;

        // Global inhibit (exercise/training mode) wins over everything and is checked before any other
        // validation so a drill can never key up an RF port by any path.
        var gate = InhibitGate;
        if (gate is not null && gate.IsTransmitInhibited)
        {
            return AgwpeTransmitResult.Failed(
                timestamp, stateAtRequest,
                gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).");
        }

        var failureReason = ValidateTransmitRequest(rawPacketLine, transmitConfirmed, rfSafetyEnabled, stateAtRequest);
        if (failureReason is not null)
        {
            return AgwpeTransmitResult.Failed(timestamp, stateAtRequest, failureReason);
        }

        var parsed = aprsParser.Parse(rawPacketLine, timestamp);
        var payload = Encoding.ASCII.GetBytes(rawPacketLine.Trim());
        var encoded = codec.Encode(
            'K',
            configuration.SelectedRadioPort,
            parsed.SourceCallsign,
            parsed.Destination,
            payload);
        var frame = codec.Decode(encoded, timestamp, configuration.SourceName);

        var current = SnapshotStream();
        if (current is null)
        {
            return AgwpeTransmitResult.Failed(timestamp, stateAtRequest, "AGWPE client is not connected.", frame);
        }

        try
        {
            await current.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            await current.FlushAsync(cancellationToken).ConfigureAwait(false);
            return AgwpeTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception);
            return AgwpeTransmitResult.Failed(timestamp, stateAtRequest, exception.Message, frame);
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
                var current = SnapshotStream();
                if (current is null)
                {
                    break;
                }

                var bytesRead = await current.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        SetState(AgwpeConnectionState.Disconnected);
                        break;
                    }

                    SetState(AgwpeConnectionState.Reconnecting);
                    pending.Clear(); // drop stale partial-frame bytes from the dead connection (transport M2)
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                    var reconnected = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                    SetStream(reconnected);
                    try { await current.DisposeAsync().ConfigureAwait(false); } catch { /* old stream already dead */ } // avoid leak (transport M4)
                    SetState(AgwpeConnectionState.Connected);
                    continue;
                }

                pending.AddRange(readBuffer.Take(bytesRead));
                var lastCompleteEnd = codec.FindLastCompleteFrameEnd(pending);
                if (lastCompleteEnd < 0)
                {
                    continue;
                }

                var completeBytes = pending.Take(lastCompleteEnd + 1).ToArray();
                pending.RemoveRange(0, lastCompleteEnd + 1);

                foreach (var frame in codec.DecodeMany(completeBytes, DateTimeOffset.UtcNow, configuration.SourceName))
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

    private void PublishFrame(AgwpeFrame frame)
    {
        receivedFrames.Writer.TryWrite(frame);
        FrameReceived?.Invoke(this, new AgwpeFrameReceivedEventArgs(frame));

        if (frame.DecodedAprsPacketText is null)
        {
            return;
        }

        var packet = new AgwpeRawPacketReceivedEventArgs(frame.DecodedAprsPacketText, frame.TimestampUtc, frame);
        receivedPackets.Writer.TryWrite(packet);
        RawPacketReceived?.Invoke(this, packet);
    }

    private string? ValidateTransmitRequest(
        string rawPacketLine,
        bool transmitConfirmed,
        bool rfSafetyEnabled,
        AgwpeConnectionState stateAtRequest)
    {
        if (!configuration.TransmitEnabled)
        {
            return "AGWPE transmit is disabled.";
        }

        if (!transmitConfirmed)
        {
            return "AGWPE transmit confirmation is required.";
        }

        if (!rfSafetyEnabled)
        {
            return "AGWPE transmit requires RF transmit safety to be explicitly enabled.";
        }

        if (configuration.SelectedRadioPort is < 0 or > 255)
        {
            return "AGWPE radio port must be between 0 and 255.";
        }

        if (stateAtRequest != AgwpeConnectionState.Connected || SnapshotStream() is null)
        {
            return "AGWPE client is not connected.";
        }

        if (string.IsNullOrWhiteSpace(rawPacketLine))
        {
            return "AGWPE packet cannot be empty.";
        }

        if (rawPacketLine.Contains('\n') || rawPacketLine.Contains('\r'))
        {
            return "AGWPE packet cannot contain line breaks.";
        }

        var parsed = aprsParser.Parse(rawPacketLine, DateTimeOffset.UtcNow);
        return parsed.IsValid ? null : string.Join(" ", parsed.ValidationErrors);
    }

    private static void ValidateConfiguration(AgwpeConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Host))
        {
            throw new ArgumentException("AGWPE host is required.", nameof(configuration));
        }

        if (configuration.Port is < 1 or > 65535)
        {
            throw new ArgumentException("AGWPE port must be between 1 and 65535.", nameof(configuration));
        }

        if (configuration.SelectedRadioPort is < 0 or > 255)
        {
            throw new ArgumentException("AGWPE radio port must be between 0 and 255.", nameof(configuration));
        }
    }

    private static async Task<Stream> CreateTcpStreamAsync(AgwpeConfiguration configuration, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(configuration.Host, configuration.Port, cancellationToken).ConfigureAwait(false);
        return tcpClient.GetStream();
    }
}
