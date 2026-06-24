namespace Aprs.Services;

public interface IGpsdClient : IAsyncDisposable
{
    event EventHandler<GpsPosition>? PositionReceived;

    GpsdConnectionState State { get; }

    Exception? LastError { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<GpsPosition> ReadPositionsAsync(CancellationToken cancellationToken);
}
