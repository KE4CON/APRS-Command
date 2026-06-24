namespace Aprs.Services;

public sealed record AprsWeatherFormatResult(
    bool IsSuccess,
    string? Packet,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings)
{
    public static AprsWeatherFormatResult Succeeded(string packet, IReadOnlyList<string> warnings)
    {
        return new AprsWeatherFormatResult(true, packet, [], warnings);
    }

    public static AprsWeatherFormatResult Failed(IReadOnlyList<string> errors, IReadOnlyList<string>? warnings = null)
    {
        return new AprsWeatherFormatResult(false, null, errors, warnings ?? []);
    }
}
