namespace Aprs.Transport;

public interface ITcpKissClient : IAsyncDisposable
{
    event EventHandler<KissFrameReceivedEventArgs>? FrameReceived;

    event EventHandler<TcpKissRawPacketReceivedEventArgs>? RawPacketReceived;

    TcpKissConnectionState State { get; }

    Exception? LastError { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<KissFrame> ReadFramesAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<TcpKissRawPacketReceivedEventArgs> ReadPacketsAsync(CancellationToken cancellationToken);

    Task<TcpKissTransmitResult> SendFrameAsync(
        int portNumber,
        KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload,
        bool transmitConfirmed,
        CancellationToken cancellationToken);
}
