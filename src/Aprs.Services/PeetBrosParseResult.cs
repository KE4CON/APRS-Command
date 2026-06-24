namespace Aprs.Services;

public sealed record PeetBrosParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static PeetBrosParseResult Failed(string error, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new PeetBrosParseResult(false, null, diagnostics ?? new Dictionary<string, string>(), error);
    }
}
