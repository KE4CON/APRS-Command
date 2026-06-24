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
    private readonly Channel<AprsIsRawPacketReceivedEventArgs> receivedPackets = Channel.CreateUnbounded<AprsIsRawPacketReceivedEventArgs>();
    private readonly AprsParser parser = new();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;
    private Stream? stream;

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

    public AprsIsConnectionState State { get; private set; } = AprsIsConnectionState.Disconnected;

    public Exception? LastError { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (State is AprsIsConnectionState.Connected or AprsIsConnectionState.Connecting)
        {
            return;
        }

        AprsIsLoginLineBuilder.Validate(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        LastError = null;
        State = AprsIsConnectionState.Connecting;

        try
        {
            stream = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            await WriteLoginLineAsync(stream, connectionCancellation.Token).ConfigureAwait(false);
            State = AprsIsConnectionState.Connected;
            receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = AprsIsConnectionState.Faulted;
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        connectionCancellation?.Cancel();

        if (stream is not null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            stream = null;
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

        State = AprsIsConnectionState.Disconnected;
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
        var stateAtRequest = State;
        var normalizedPacket = rawPacketLine?.Trim() ?? string.Empty;
        var failureReason = ValidateTransmitRequest(normalizedPacket, transmitConfirmed, stateAtRequest);
        if (failureReason is not null)
        {
            return AprsIsTransmitResult.Failed(timestamp, normalizedPacket, stateAtRequest, failureReason);
        }

        try
        {
            var bytes = Encoding.ASCII.GetBytes(normalizedPacket + "\r\n");
            await stream!.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return AprsIsTransmitResult.Succeeded(timestamp, normalizedPacket, stateAtRequest);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = AprsIsConnectionState.Faulted;
            return AprsIsTransmitResult.Failed(timestamp, normalizedPacket, stateAtRequest, exception.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        connectionCancellation?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && stream is not null)
            {
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
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

                if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                {
                    State = AprsIsConnectionState.Disconnected;
                    break;
                }

                State = AprsIsConnectionState.Reconnecting;
                await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                stream = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                await WriteLoginLineAsync(stream, cancellationToken).ConfigureAwait(false);
                State = AprsIsConnectionState.Connected;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            LastError = exception;
            State = AprsIsConnectionState.Faulted;
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
        AprsIsConnectionState stateAtRequest)
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

        if (stateAtRequest != AprsIsConnectionState.Connected || stream is null)
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
