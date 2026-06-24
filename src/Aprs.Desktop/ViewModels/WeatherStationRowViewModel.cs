using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherStationRowViewModel
{
    public WeatherStationRowViewModel(WeatherStationDisplayRecord record, DateTimeOffset now)
    {
        StationId = record.StationId;
        DisplayName = record.DisplayName;
        SourceType = FormatEnum(record.SourceType);
        Origin = FormatEnum(record.Origin);
        LastUpdate = record.LastUpdateUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        Age = FormatAge(now - record.LastUpdateUtc);
        DataState = record.DataState.ToString();
        StaleWarning = record.DataState == WeatherDataState.Current ? "Current" : $"{record.DataState} weather data";
        Coordinates = FormatCoordinates(record.Latitude, record.Longitude);
        Temperature = record.TemperatureFahrenheit is null ? "-" : $"{record.TemperatureFahrenheit} F";
        Wind = FormatWind(record.WindDirectionDegrees, record.WindSpeedMph, record.WindGustMph);
        Rain = FormatRain(record.RainLastHourHundredthsInch, record.RainLast24HoursHundredthsInch, record.RainSinceMidnightHundredthsInch);
        Humidity = record.HumidityPercent is null ? "-" : $"{record.HumidityPercent}%";
        Pressure = record.BarometricPressureMillibars is null ? "-" : $"{record.BarometricPressureMillibars:0.0} mb";
        Solar = record.LuminosityWattsPerSquareMeter is null ? "-" : $"{record.LuminosityWattsPerSquareMeter} W/m2";
        Uv = record.UvIndex is null ? "-" : $"{record.UvIndex:0.0}";
        SolarUv = $"{Solar} / {Uv}";
        Snow = record.SnowHundredthsInch is null ? "-" : $"{record.SnowHundredthsInch / 100.0:0.00} in";
        Lightning = string.IsNullOrWhiteSpace(record.LightningEventInformation) ? "-" : record.LightningEventInformation;
        RawPayload = string.IsNullOrWhiteSpace(record.RawPayload) ? "-" : record.RawPayload;
    }

    public string StationId { get; }

    public string DisplayName { get; }

    public string SourceType { get; }

    public string Origin { get; }

    public string LastUpdate { get; }

    public string Age { get; }

    public string DataState { get; }

    public string StaleWarning { get; }

    public string Coordinates { get; }

    public string Temperature { get; }

    public string Wind { get; }

    public string Rain { get; }

    public string Humidity { get; }

    public string Pressure { get; }

    public string Solar { get; }

    public string Uv { get; }

    public string SolarUv { get; }

    public string Snow { get; }

    public string Lightning { get; }

    public string RawPayload { get; }

    private static string FormatCoordinates(double? latitude, double? longitude)
    {
        return latitude is null || longitude is null
            ? "-"
            : $"{latitude:0.00000}, {longitude:0.00000}";
    }

    private static string FormatWind(int? direction, int? speed, int? gust)
    {
        if (direction is null && speed is null && gust is null)
        {
            return "-";
        }

        var directionText = direction is null ? "---" : $"{direction} deg";
        var speedText = speed is null ? "--" : $"{speed} mph";
        var gustText = gust is null ? "gust --" : $"gust {gust} mph";
        return $"{directionText} / {speedText} / {gustText}";
    }

    private static string FormatRain(int? lastHour, int? last24Hours, int? sinceMidnight)
    {
        if (lastHour is null && last24Hours is null && sinceMidnight is null)
        {
            return "-";
        }

        return $"1h {FormatHundredths(lastHour)}, 24h {FormatHundredths(last24Hours)}, mid {FormatHundredths(sinceMidnight)}";
    }

    private static string FormatHundredths(int? value)
    {
        return value is null ? "--" : $"{value.Value / 100.0:0.00} in";
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

    private static string FormatEnum<T>(T value)
        where T : struct, Enum
    {
        return value.ToString();
    }
}
