namespace Aprs.Services;

public sealed record CommonWeatherObservation(
    string SourceName,
    WeatherObservationSourceType SourceType,
    string? StationDeviceId,
    string? Callsign,
    DateTimeOffset TimestampUtc,
    double? Latitude,
    double? Longitude,
    int? WindDirectionDegrees,
    double? WindSpeedMph,
    double? WindGustMph,
    double? TemperatureFahrenheit,
    double? RainLastHourInches,
    double? RainLast24HoursInches,
    double? RainSinceMidnightInches,
    int? HumidityPercent,
    double? BarometricPressureMillibars,
    int? LuminosityWattsPerSquareMeter,
    double? UvIndex,
    double? SnowInches,
    int? LightningCount,
    double? LightningDistanceMiles,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? RawSourcePayload,
    WeatherDataState StaleDataState,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings)
{
    public bool HasPosition => Latitude is not null && Longitude is not null;
}
