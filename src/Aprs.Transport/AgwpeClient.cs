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
    private readonly Channel<AgwpeFrame> receivedFrames = Channel.CreateUnbounded<AgwpeFrame>();
    private readonly Channel<AgwpeRawPacketReceivedEventArgs> receivedPackets = Channel.CreateUnbounded<AgwpeRawPacketReceivedEventArgs>();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;
    private Stream? stream;

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

    public AgwpeConnectionState State { get; private set; } = AgwpeConnectionState.Disconnected;

    public Exception? LastError { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            State = AgwpeConnectionState.Disconnected;
            return;
        }

        if (State is AgwpeConnectionState.Connected or AgwpeConnectionState.Connecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        State = AgwpeConnectionState.Connecting;
        LastError = null;

        try
        {
            stream = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            State = AgwpeConnectionState.Connected;
            if (configuration.ReceiveEnabled)
            {
                receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = AgwpeConnectionState.Faulted;
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

        State = AgwpeConnectionState.Disconnected;
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

        try
        {
            await stream!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return AgwpeTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = AgwpeConnectionState.Faulted;
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
            while (!cancellationToken.IsCancellationRequested && stream is not null)
            {
                var bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        State = AgwpeConnectionState.Disconnected;
                        break;
                    }

                    State = AgwpeConnectionState.Reconnecting;
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                    stream = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                    State = AgwpeConnectionState.Connected;
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
            LastError = exception;
            State = AgwpeConnectionState.Faulted;
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

        if (stateAtRequest != AgwpeConnectionState.Connected || stream is null)
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
