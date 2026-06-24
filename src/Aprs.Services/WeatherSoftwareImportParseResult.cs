namespace Aprs.Services;

public sealed record WeatherSoftwareImportParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static WeatherSoftwareImportParseResult Failed(string error, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new WeatherSoftwareImportParseResult(false, null, diagnostics ?? new Dictionary<string, string>(), error);
    }
}
