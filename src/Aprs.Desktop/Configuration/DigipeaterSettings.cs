namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted operator-facing digipeater settings. Covers the enable toggle and the
/// key path and mode options an operator would configure. The full
/// <see cref="Aprs.Services.DigipeaterConfiguration"/> lives at the service layer.
/// </summary>
public sealed record DigipeaterSettings(
    bool Enabled,
    bool RfTransmitEnabled,
    string DigipeaterCallsign,
    bool FillInMode,
    bool FullMode,
    string SupportedAliases,
    string? RfTransmitPort)
{
    public static DigipeaterSettings Default { get; } = new(
        Enabled:             false,
        RfTransmitEnabled:   false,
        DigipeaterCallsign:  string.Empty,
        FillInMode:          false,
        FullMode:            false,
        SupportedAliases:    "WIDE1-1,WIDE2-1",
        RfTransmitPort:      null);
}
