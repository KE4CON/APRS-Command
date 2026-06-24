namespace Aprs.Services;

public sealed record DavisWeatherParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static DavisWeatherParseResult Failed(string error, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new DavisWeatherParseResult(false, null, diagnostics ?? new Dictionary<string, string>(), error);
    }
}
