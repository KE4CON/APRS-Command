using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Aprs.Core;

namespace Aprs.Transport;

public sealed class AprsIsClient : IAprsIsClient
{
    private readonly AprsIsClientConfiguration configuration;
    private readonly Func<AprsIsClientConfiguration, CancellationToken, Task<Stream>> streamFactory;
    private readonly Channel<AprsIsRawPacketReceivedEventArgs> receivedPackets = Channel.CreateBounded<AprsIsRawPacketReceivedEventArgs>(new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly AprsParser parser = new();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;

    // Guards the mutable stream reference and the State/LastError fields, which are otherwise read by
    // the send path while the receive loop reassigns them during a reconnect. Never held across an
    // await: callers snapshot the stream under the lock, then do I/O on the snapshot.
    private readonly object sync = new();
    private Stream? stream;
    private AprsIsConnectionState state = AprsIsConnectionState.Disconnected;
    private Exception? lastError;

    public AprsIsClient(AprsIsClientConfiguration configuration)
        : this(configuration, CreateTcpStreamAsync)
    {
    }

    public AprsIsClient(
        AprsIsClientConfiguration configuration,
        Func<AprsIsClientConfiguration, CancellationToken, Task<Stream>> streamFactory)
    {
        this.configuration = configuration;
        this.streamFactory = streamFactory;
    }

    public event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived;

    /// <summary>
    /// Optional global transmit-inhibit gate. When set and inhibited (for example exercise mode),
    /// every <see cref="SendRawPacketAsync"/> call is blocked before any bytes reach the socket,
    /// regardless of the higher-level path that requested the transmit.
    /// </summary>
    public ITransmitInhibitGate? InhibitGate { get; set; }

    public AprsIsConnectionState State { get { lock (sync) { return state; } } }

    public Exception? LastError { get { lock (sync) { return lastError; } } }

    private void SetState(AprsIsConnectionState value) { lock (sync) { state = value; } }

    private void Fault(Exception exception)
    {
        lock (sync) { lastError = exception; state = AprsIsConnectionState.Faulted; }
    }

    private void SetStream(Stream? value) { lock (sync) { stream = value; } }

    private (Stream? Stream, AprsIsConnectionState State) Snapshot()
    {
        lock (sync) { return (stream, state); }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (State is AprsIsConnectionState.Connected or AprsIsConnectionState.Connecting)
        {
            return;
        }

        AprsIsLoginLineBuilder.Validate(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (sync) { lastError = null; state = AprsIsConnectionState.Connecting; }

        try
        {
            var opened = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            SetStream(opened);
            await WriteLoginLineAsync(opened, connectionCancellation.Token).ConfigureAwait(false);

            // Wait for the server's logresp line before marking Connected.
            // APRS-IS sends "# logresp CALLSIGN verified, server ..." or "unverified".
            // Packets sent before this acknowledgment are silently discarded.
            await WaitForLogrespAsync(opened, connectionCancellation.Token).ConfigureAwait(false);

            SetState(AprsIsConnectionState.Connected);
            receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
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

        SetState(AprsIsConnectionState.Disconnected);
    }

    public async IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await receivedPackets.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (receivedPackets.Reader.TryRead(out var packet))
            {
                yield return packet;
            }
        }
    }

    public async Task<AprsIsTransmitResult> SendRawPacketAsync(
        string rawPacketLine,
        bool transmitConfirmed,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        // Consistent snapshot so a concurrent reconnect cannot swap the stream between the write and
        // the flush, and so validation and I/O see the same stream.
        var (active, stateAtRequest) = Snapshot();
        var normalizedPacket = rawPacketLine?.Trim() ?? string.Empty;

        // Global inhibit (exercise/training mode) wins over everything and is checked before any
        // other validation so a drill can never key up APRS-IS by any path.
        var gate = InhibitGate;
        if (gate is not null && gate.IsTransmitInhibited)
        {
            return AprsIsTransmitResult.Failed(
                timestamp, normalizedPacket, stateAtRequest,
                gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).");
        }

        var failureReason = ValidateTransmitRequest(normalizedPacket, transmitConfirmed, stateAtRequest, active);
        if (failureReason is not null)
        {
            return AprsIsTransmitResult.Failed(timestamp, normalizedPacket, stateAtRequest, failureReason);
        }

        try
        {
            var bytes = Encoding.ASCII.GetBytes(normalizedPacket + "\r\n");
            await active!.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await active.FlushAsync(cancellationToken).ConfigureAwait(false);

            return AprsIsTransmitResult.Succeeded(timestamp, normalizedPacket, stateAtRequest);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception);
            return AprsIsTransmitResult.Failed(timestamp, normalizedPacket, stateAtRequest, exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        connectionCancellation?.Dispose();
    }

    private async Task WaitForLogrespAsync(Stream targetStream, CancellationToken cancellationToken)
    {
        // Read server lines until we find the logresp acknowledgment.
        // APRS-IS sends "# logresp CALLSIGN verified, server ..." or "unverified".
        // Packets sent before this acknowledgment are silently discarded by the server.
        // Times out after 5 seconds and proceeds regardless — better to attempt
        // transmit than to hang if the server is slow or non-standard.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var buf = new byte[1024];
            var sb  = new System.Text.StringBuilder();
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var n = await targetStream.ReadAsync(buf, timeoutCts.Token).ConfigureAwait(false);
                if (n == 0) break;
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                var text = sb.ToString();
                if (text.Contains("# logresp", StringComparison.OrdinalIgnoreCase))
                {
                    // Push any extra data (already-arrived packets) into the pending buffer.
                    var logrespEnd = text.IndexOf('\n', text.IndexOf("# logresp", StringComparison.OrdinalIgnoreCase));
                    if (logrespEnd >= 0 && logrespEnd + 1 < text.Length)
                        pendingData = text[(logrespEnd + 1)..];
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out — proceed anyway.
        }
    }

    private string pendingData = string.Empty;

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Process any data that arrived before the receive loop started
            // (buffered during WaitForLogrespAsync).
            if (!string.IsNullOrEmpty(pendingData))
            {
                foreach (var pendingLine in pendingData.Split('\n'))
                {
                    var trimmed = pendingLine.TrimEnd('\r');
                    if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
                        PublishPacket(trimmed);
                }
                pendingData = string.Empty;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                Stream? active;
                lock (sync) { active = stream; }
                if (active is null) break;

                using (var reader = new StreamReader(active, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line is null)
                        {
                            break;
                        }

                        if (line.StartsWith('#'))
                        {
                            continue;
                        }

                        PublishPacket(line);
                    }
                }

                if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                {
                    SetState(AprsIsConnectionState.Disconnected);
                    break;
                }

                SetState(AprsIsConnectionState.Reconnecting);
                await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);

                // Dispose the closed stream (the one we were reading from) before replacing it —
                // otherwise each reconnect leaks the previous NetworkStream/socket.
                try { await active.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort cleanup of the dead stream */ }

                var reopened = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                await WriteLoginLineAsync(reopened, cancellationToken).ConfigureAwait(false);
                await WaitForLogrespAsync(reopened, cancellationToken).ConfigureAwait(false);
                SetStream(reopened);
                SetState(AprsIsConnectionState.Connected);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            Fault(exception);
        }
    }

    private void PublishPacket(string line)
    {
        var packet = new AprsIsRawPacketReceivedEventArgs(line, DateTimeOffset.UtcNow);
        receivedPackets.Writer.TryWrite(packet);
        RawPacketReceived?.Invoke(this, packet);
    }

    private string? ValidateTransmitRequest(
        string rawPacketLine,
        bool transmitConfirmed,
        AprsIsConnectionState stateAtRequest,
        Stream? activeStream)
    {
        if (!configuration.TransmitEnabled)
        {
            return "APRS-IS transmit is disabled.";
        }

        if (configuration.ReceiveOnly)
        {
            return "APRS-IS client is configured for receive-only operation.";
        }

        if (configuration.RequireTransmitConfirmation && !transmitConfirmed)
        {
            return "APRS-IS transmit confirmation is required.";
        }

        if (string.IsNullOrWhiteSpace(configuration.Callsign))
        {
            return "APRS-IS callsign is required before transmit.";
        }

        if (!IsValidTransmitPasscode(configuration.Passcode))
        {
            return "A valid APRS-IS passcode is required before transmit.";
        }

        if (stateAtRequest != AprsIsConnectionState.Connected || activeStream is null)
        {
            return "APRS-IS client is not connected.";
        }

        if (string.IsNullOrWhiteSpace(rawPacketLine))
        {
            return "APRS packet cannot be empty.";
        }

        if (rawPacketLine.Contains('\r') || rawPacketLine.Contains('\n'))
        {
            return "APRS packet cannot contain line breaks.";
        }

        var parsed = parser.Parse(rawPacketLine, DateTimeOffset.UtcNow);
        if (!parsed.IsValid)
        {
            return parsed.ValidationErrors.FirstOrDefault() ?? "APRS packet is malformed.";
        }

        return null;
    }

    private static bool IsValidTransmitPasscode(string passcode)
    {
        return int.TryParse(passcode?.Trim(), out var parsedPasscode) && parsedPasscode > 0;
    }

    private async Task WriteLoginLineAsync(Stream targetStream, CancellationToken cancellationToken)
    {
        var loginLine = AprsIsLoginLineBuilder.Build(configuration) + "\r\n";
        var bytes = Encoding.ASCII.GetBytes(loginLine);
        await targetStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await targetStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Stream> CreateTcpStreamAsync(
        AprsIsClientConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(configuration.ServerHost, configuration.ServerPort, cancellationToken).ConfigureAwait(false);

        return tcpClient.GetStream();
    }
}
