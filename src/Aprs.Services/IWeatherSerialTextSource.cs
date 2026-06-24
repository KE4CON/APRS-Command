namespace Aprs.Services;

public interface IWeatherSerialTextSource : IAsyncDisposable
{
    bool IsOpen { get; }

    Task OpenAsync(PeetBrosConfiguration configuration, CancellationToken cancellationToken = default);

    Task CloseAsync(CancellationToken cancellationToken = default);

    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}
