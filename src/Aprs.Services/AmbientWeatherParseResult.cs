namespace Aprs.Services;

public sealed record AmbientWeatherParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static AmbientWeatherParseResult Failed(string error, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new AmbientWeatherParseResult(false, null, diagnostics ?? new Dictionary<string, string>(), error);
    }
}
