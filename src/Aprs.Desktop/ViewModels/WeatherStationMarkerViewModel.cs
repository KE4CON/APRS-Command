using Aprs.Mapping;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherStationMarkerViewModel
{
    private const double LongitudeMin = -180;
    private const double LongitudeMax = 180;
    private const double LatitudeMin = -90;
    private const double LatitudeMax = 90;

    public WeatherStationMarkerViewModel(WeatherStationMarker marker)
    {
        StationId = marker.StationId;
        DisplayName = marker.DisplayName;
        SourceType = marker.SourceType;
        Latitude = marker.Latitude;
        Longitude = marker.Longitude;
        DataState = marker.DataState;
        Origin = marker.Origin;
        LastUpdateUtc = marker.LastUpdateUtc;
        TemperatureFahrenheit = marker.TemperatureFahrenheit;
        WindDirectionDegrees = marker.WindDirectionDegrees;
        WindSpeedMph = marker.WindSpeedMph;
        WindGustMph = marker.WindGustMph;
    }

    public string StationId { get; }

    public string DisplayName { get; }

    public WeatherStationSourceType SourceType { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public WeatherDataState DataState { get; }

    public WeatherStationOrigin Origin { get; }

    public DateTimeOffset LastUpdateUtc { get; }

    public int? TemperatureFahrenheit { get; }

    public int? WindDirectionDegrees { get; }

    public int? WindSpeedMph { get; }

    public int? WindGustMph { get; }

    public string SymbolLabel => "WX";

    public string Tooltip => $"{DisplayName} {FormatTemperature()} {FormatWind()}";

    public bool IsStale => DataState != WeatherDataState.Current;

    public double MapLeftPercent => Normalize(Longitude, LongitudeMin, LongitudeMax) * 100;

    public double MapTopPercent => (1 - Normalize(Latitude, LatitudeMin, LatitudeMax)) * 100;

    private string FormatTemperature()
    {
        return TemperatureFahrenheit is null ? "-- F" : $"{TemperatureFahrenheit} F";
    }

    private string FormatWind()
    {
        if (WindDirectionDegrees is null && WindSpeedMph is null)
        {
            return "wind unknown";
        }

        return $"{WindDirectionDegrees?.ToString() ?? "---"} deg/{WindSpeedMph?.ToString() ?? "--"} mph";
    }

    private static double Normalize(double value, double minimum, double maximum)
    {
        var normalized = (value - minimum) / (maximum - minimum);
        return Math.Clamp(normalized, 0, 1);
    }
}
