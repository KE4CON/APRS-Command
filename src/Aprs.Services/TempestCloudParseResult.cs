namespace Aprs.Services;

public sealed record TempestCloudParseResult(
    bool IsHandled,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static TempestCloudParseResult Failed(string error)
    {
        return new TempestCloudParseResult(false, null, new Dictionary<string, string>(), error);
    }
}
