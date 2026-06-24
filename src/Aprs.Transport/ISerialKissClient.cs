namespace Aprs.Transport;

public interface ISerialKissClient : IAsyncDisposable
{
    event EventHandler<KissFrameReceivedEventArgs>? FrameReceived;

    event EventHandler<TcpKissRawPacketReceivedEventArgs>? RawPacketReceived;

    SerialKissConnectionState State { get; }

    Exception? LastError { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<KissFrame> ReadFramesAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<TcpKissRawPacketReceivedEventArgs> ReadPacketsAsync(CancellationToken cancellationToken);

    Task<SerialKissTransmitResult> SendFrameAsync(
        int portNumber,
        KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload,
        bool transmitConfirmed,
        bool rfSafetyEnabled,
        CancellationToken cancellationToken);
}
