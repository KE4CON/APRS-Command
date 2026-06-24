namespace Aprs.Services;

public sealed record WeatherBeaconPreviewResult(
    bool IsSuccess,
    string? Packet,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    bool AprsIsEligible,
    bool RfEligible,
    bool IsStale,
    string? SelectedSourceName)
{
    public static WeatherBeaconPreviewResult Failed(
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null,
        bool isStale = false,
        string? selectedSourceName = null)
    {
        return new WeatherBeaconPreviewResult(
            IsSuccess: false,
            Packet: null,
            ValidationErrors: errors,
            ValidationWarnings: warnings ?? [],
            AprsIsEligible: false,
            RfEligible: false,
            IsStale: isStale,
            SelectedSourceName: selectedSourceName);
    }
}
