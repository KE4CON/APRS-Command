namespace Aprs.Services;

public sealed record AprsBeaconFormatResult(
    bool IsSuccess,
    string? Packet,
    IReadOnlyList<string> ValidationErrors)
{
    public static AprsBeaconFormatResult Succeeded(string packet)
    {
        return new AprsBeaconFormatResult(true, packet, []);
    }

    public static AprsBeaconFormatResult Failed(IReadOnlyList<string> validationErrors)
    {
        return new AprsBeaconFormatResult(false, null, validationErrors);
    }
}
