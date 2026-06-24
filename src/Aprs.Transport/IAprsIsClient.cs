namespace Aprs.Transport;

public interface IAprsIsClient : IAsyncDisposable
{
    event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived;

    AprsIsConnectionState State { get; }

    Exception? LastError { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends one raw APRS packet line to APRS-IS when transmit is explicitly enabled and confirmed.
    /// </summary>
    Task<AprsIsTransmitResult> SendRawPacketAsync(
        string rawPacketLine,
        bool transmitConfirmed,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(CancellationToken cancellationToken);
}
