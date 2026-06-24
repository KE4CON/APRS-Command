namespace Aprs.Services;

public sealed record TempestUdpParseResult(
    bool IsHandled,
    string MessageType,
    CommonWeatherObservation? Observation,
    IReadOnlyDictionary<string, string> Diagnostics,
    string? Error)
{
    public static TempestUdpParseResult Ignored(string messageType, IReadOnlyDictionary<string, string>? diagnostics = null)
    {
        return new TempestUdpParseResult(true, messageType, null, diagnostics ?? new Dictionary<string, string>(), null);
    }

    public static TempestUdpParseResult Failed(string error)
    {
        return new TempestUdpParseResult(false, "unknown", null, new Dictionary<string, string>(), error);
    }
}
