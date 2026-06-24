namespace Aprs.Services;

public sealed record WeatherSoftwareFieldMapping(
    string SourceField,
    string TargetWeatherField,
    string? Unit,
    string? Notes);
