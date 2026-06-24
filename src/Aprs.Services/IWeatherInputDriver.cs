namespace Aprs.Services;

/// <summary>
/// Provides normalized weather observations from one local, network, file, or simulated weather source.
/// </summary>
public interface IWeatherInputDriver
{
    string DriverId { get; }

    string DriverName { get; }

    WeatherInputDriverType DriverType { get; }

    bool Enabled { get; }

    WeatherInputDriverStatus Status { get; }

    CommonWeatherObservation? LastObservation { get; }

    Exception? LastError { get; }

    WeatherObservationValidationResult LastValidationResult { get; }

    WeatherInputDriverConfiguration Configuration { get; }

    event EventHandler<WeatherObservationReceivedEventArgs>? ObservationReceived;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
