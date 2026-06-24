namespace Aprs.Services;

public sealed record WeatherInputDriverSnapshot(
    string DriverId,
    string DriverName,
    WeatherInputDriverType DriverType,
    bool Enabled,
    WeatherInputDriverStatus Status,
    CommonWeatherObservation? LastObservation,
    DateTimeOffset? LastObservationTimeUtc,
    Exception? LastError,
    WeatherObservationValidationResult LastValidationResult,
    WeatherInputDriverConfiguration Configuration);
