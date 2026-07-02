namespace Aprs.Services;

/// <summary>
/// A timestamped snapshot of weather readings used for the weather history graph.
/// Stored in a rolling ring buffer per station.
/// </summary>
public sealed record WeatherHistoryRecord(
    DateTimeOffset  Timestamp,
    double?         TemperatureFahrenheit,
    double?         HumidityPercent,
    double?         PressureMillibars,
    double?         WindSpeedMph,
    double?         WindGustMph,
    int?            WindDirectionDegrees,
    double?         RainLastHourInches);
