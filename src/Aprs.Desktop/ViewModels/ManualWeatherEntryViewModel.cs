namespace Aprs.Desktop.ViewModels;

public sealed class ManualWeatherEntryViewModel
{
    public string SourceName { get; set; } = "Manual Weather";

    public DateTimeOffset? TimestampUtc { get; set; }

    public double? TemperatureFahrenheit { get; set; }

    public int? HumidityPercent { get; set; }

    public int? WindDirectionDegrees { get; set; }

    public double? WindSpeedMph { get; set; }

    public double? WindGustMph { get; set; }

    public double? RainLastHourInches { get; set; }

    public double? RainLast24HoursInches { get; set; }

    public double? RainSinceMidnightInches { get; set; }

    public double? BarometricPressureMillibars { get; set; }

    public IReadOnlyList<string> ValidationErrors { get; private set; } = [];

    public string ValidationStatus { get; private set; } = "Not validated";

    public bool Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SourceName))
        {
            errors.Add("Source name is required.");
        }

        if (TimestampUtc is null)
        {
            errors.Add("Timestamp is required.");
        }

        if (TemperatureFahrenheit is null)
        {
            errors.Add("Temperature is required.");
        }

        if (HumidityPercent is null)
        {
            errors.Add("Humidity is required.");
        }

        if (WindDirectionDegrees is null)
        {
            errors.Add("Wind direction is required.");
        }

        if (WindSpeedMph is null)
        {
            errors.Add("Wind speed is required.");
        }

        if (WindGustMph is null)
        {
            errors.Add("Wind gust is required.");
        }

        if (BarometricPressureMillibars is null)
        {
            errors.Add("Barometric pressure is required.");
        }

        ValidationErrors = errors;
        ValidationStatus = errors.Count == 0 ? "Valid" : string.Join("; ", errors);
        return errors.Count == 0;
    }
}
