namespace Aprs.Transport;

public interface IAgwpeClient : IAsyncDisposable
{
    event EventHandler<AgwpeFrameReceivedEventArgs>? FrameReceived;

    event EventHandler<AgwpeRawPacketReceivedEventArgs>? RawPacketReceived;

    AgwpeConnectionState State { get; }

    Exception? LastError { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<AgwpeFrame> ReadFramesAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<AgwpeRawPacketReceivedEventArgs> ReadPacketsAsync(CancellationToken cancellationToken);

    Task<AgwpeTransmitResult> SendPacketAsync(string rawPacketLine, bool transmitConfirmed, bool rfSafetyEnabled, CancellationToken cancellationToken);
}
