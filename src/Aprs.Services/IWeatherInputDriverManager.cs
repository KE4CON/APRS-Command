namespace Aprs.Services;

/// <summary>
/// Coordinates weather input drivers and forwards validated observations into the weather display layer.
/// </summary>
public interface IWeatherInputDriverManager
{
    void RegisterDriver(IWeatherInputDriver driver);

    bool UnregisterDriver(string driverId);

    IReadOnlyCollection<WeatherInputDriverSnapshot> GetAllDrivers();

    IReadOnlyCollection<WeatherInputDriverSnapshot> GetEnabledDrivers();

    WeatherInputDriverSnapshot? GetDriver(string driverId);

    Task<bool> StartDriverAsync(string driverId, CancellationToken cancellationToken = default);

    Task<bool> StopDriverAsync(string driverId, CancellationToken cancellationToken = default);

    Task StartEnabledDriversAsync(CancellationToken cancellationToken = default);

    Task StopAllDriversAsync(CancellationToken cancellationToken = default);
}
