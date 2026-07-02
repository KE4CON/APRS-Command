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
    private readonly Channel<KissFrame> receivedFrames = Channel.CreateUnbounded<KissFrame>();
    private readonly Channel<TcpKissRawPacketReceivedEventArgs> receivedPackets = Channel.CreateUnbounded<TcpKissRawPacketReceivedEventArgs>();
    private CancellationTokenSource? connectionCancellation;
    private Task? receiveTask;
    private Stream? stream;

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

    public TcpKissConnectionState State { get; private set; } = TcpKissConnectionState.Disconnected;

    public Exception? LastError { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            State = TcpKissConnectionState.Disconnected;
            return;
        }

        if (State is TcpKissConnectionState.Connected or TcpKissConnectionState.Connecting)
        {
            return;
        }

        ValidateConfiguration(configuration);
        connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        State = TcpKissConnectionState.Connecting;
        LastError = null;

        try
        {
            stream = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
            State = TcpKissConnectionState.Connected;
            if (configuration.ReceiveEnabled)
            {
                receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = TcpKissConnectionState.Faulted;
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

        State = TcpKissConnectionState.Disconnected;
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
        var stateAtRequest = State;
        var failureReason = ValidateTransmitRequest(portNumber, commandType, ax25Payload, transmitConfirmed, stateAtRequest);
        if (failureReason is not null)
        {
            return TcpKissTransmitResult.Failed(timestamp, stateAtRequest, failureReason);
        }

        var encoded = KissFrameCodec.Encode(portNumber, commandType, ax25Payload);
        var frame = KissFrameCodec.Decode(encoded, timestamp, configuration.SourceName, payloadDecoder);

        try
        {
            await stream!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return TcpKissTransmitResult.Succeeded(timestamp, stateAtRequest, frame);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception;
            State = TcpKissConnectionState.Faulted;
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
            while (!cancellationToken.IsCancellationRequested && stream is not null)
            {
                var bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
                    {
                        State = TcpKissConnectionState.Disconnected;
                        break;
                    }

                    State = TcpKissConnectionState.Reconnecting;
                    await Task.Delay(configuration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                    stream = await streamFactory(configuration, cancellationToken).ConfigureAwait(false);
                    State = TcpKissConnectionState.Connected;
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
            State = TcpKissConnectionState.Faulted;
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
        TcpKissConnectionState stateAtRequest)
    {
        if (!configuration.TransmitEnabled)
        {
            return "TCP KISS transmit is disabled.";
        }

        if (!transmitConfirmed)
        {
            return "TCP KISS transmit confirmation is required.";
        }

        if (stateAtRequest != TcpKissConnectionState.Connected || stream is null)
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
