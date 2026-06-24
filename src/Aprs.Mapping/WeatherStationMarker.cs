using Aprs.Services;

namespace Aprs.Mapping;

public sealed record WeatherStationMarker(
    string StationId,
    string DisplayName,
    WeatherStationSourceType SourceType,
    double Latitude,
    double Longitude,
    WeatherDataState DataState,
    WeatherStationOrigin Origin,
    DateTimeOffset LastUpdateUtc,
    int? TemperatureFahrenheit,
    int? WindDirectionDegrees,
    int? WindSpeedMph,
    int? WindGustMph)
{
    public static bool TryCreate(WeatherStationDisplayRecord station, out WeatherStationMarker? marker)
    {
        if (station.Latitude is null || station.Longitude is null)
        {
            marker = null;
            return false;
        }

        marker = new WeatherStationMarker(
            station.StationId,
            station.DisplayName,
            station.SourceType,
            station.Latitude.Value,
            station.Longitude.Value,
            station.DataState,
            station.Origin,
            station.LastUpdateUtc,
            station.TemperatureFahrenheit,
            station.WindDirectionDegrees,
            station.WindSpeedMph,
            station.WindGustMph);
        return true;
    }
}
