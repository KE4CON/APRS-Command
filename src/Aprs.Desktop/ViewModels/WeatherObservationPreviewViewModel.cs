using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherObservationPreviewViewModel
{
    public WeatherObservationPreviewViewModel(CommonWeatherObservation? observation, DateTimeOffset now)
    {
        SourceName = Missing(observation?.SourceName);
        SourceType = observation?.SourceType.ToString() ?? "Unknown";
        StationDeviceId = Missing(observation?.StationDeviceId);
        Timestamp = observation?.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "-";
        DataAge = observation is null ? "-" : FormatAge(now - observation.TimestampUtc);
        DataState = observation?.StaleDataState.ToString() ?? "Unknown";
        StaleWarning = observation is null
            ? "No observation has been received."
            : observation.StaleDataState == WeatherDataState.Current ? "Current" : $"{observation.StaleDataState} weather data";
        Wind = FormatWind(observation?.WindDirectionDegrees, observation?.WindSpeedMph, observation?.WindGustMph);
        Temperature = observation?.TemperatureFahrenheit is null ? "-" : $"{observation.TemperatureFahrenheit:0.0} F";
        Humidity = observation?.HumidityPercent is null ? "-" : $"{observation.HumidityPercent}%";
        Pressure = observation?.BarometricPressureMillibars is null ? "-" : $"{observation.BarometricPressureMillibars:0.0} mb";
        Rain = FormatRain(observation?.RainLastHourInches, observation?.RainLast24HoursInches, observation?.RainSinceMidnightInches);
        SolarLuminosity = observation?.LuminosityWattsPerSquareMeter is null ? "-" : $"{observation.LuminosityWattsPerSquareMeter} W/m2";
        Uv = observation?.UvIndex is null ? "-" : $"{observation.UvIndex:0.0}";
        Lightning = FormatLightning(observation?.LightningCount, observation?.LightningDistanceMiles);
        Diagnostics = observation?.Diagnostics.Count > 0
            ? string.Join(Environment.NewLine, observation.Diagnostics.Select(item => $"{item.Key}: {item.Value}"))
            : "-";
        RawPayload = Missing(observation?.RawSourcePayload);
    }

    public string SourceName { get; }

    public string SourceType { get; }

    public string StationDeviceId { get; }

    public string Timestamp { get; }

    public string DataAge { get; }

    public string DataState { get; }

    public string StaleWarning { get; }

    public string Wind { get; }

    public string Temperature { get; }

    public string Humidity { get; }

    public string Pressure { get; }

    public string Rain { get; }

    public string SolarLuminosity { get; }

    public string Uv { get; }

    public string Lightning { get; }

    public string Diagnostics { get; }

    public string RawPayload { get; }

    private static string Missing(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatWind(int? direction, double? speed, double? gust)
    {
        if (direction is null && speed is null && gust is null)
        {
            return "-";
        }

        return $"{(direction is null ? "---" : $"{direction} deg")} / {(speed is null ? "--" : $"{speed:0.0} mph")} / gust {(gust is null ? "--" : $"{gust:0.0} mph")}";
    }

    private static string FormatRain(double? lastHour, double? last24Hours, double? sinceMidnight)
    {
        if (lastHour is null && last24Hours is null && sinceMidnight is null)
        {
            return "-";
        }

        return $"1h {FormatInches(lastHour)}, 24h {FormatInches(last24Hours)}, mid {FormatInches(sinceMidnight)}";
    }

    private static string FormatInches(double? value)
    {
        return value is null ? "--" : $"{value:0.00} in";
    }

    private static string FormatLightning(int? count, double? distance)
    {
        return (count, distance) switch
        {
            (null, null) => "-",
            (not null, null) => $"{count} strikes",
            (null, not null) => $"{distance:0.0} mi",
            _ => $"{count} strikes, {distance:0.0} mi"
        };
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1)
        {
            return "just now";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} min ago";
        }

        return age.TotalDays < 1 ? $"{(int)age.TotalHours} hr ago" : $"{(int)age.TotalDays} days ago";
    }
}
