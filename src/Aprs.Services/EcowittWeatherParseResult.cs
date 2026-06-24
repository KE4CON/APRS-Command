namespace Aprs.Services;

public sealed record EcowittWeatherParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static EcowittWeatherParseResult Failed(string error, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new EcowittWeatherParseResult(false, null, diagnostics ?? new Dictionary<string, string>(), error);
    }
}
