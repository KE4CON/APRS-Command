namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted operator-facing iGate settings. Covers the enable toggle and the
/// key RF-to-APRS-IS gating options an operator would actually configure.
/// The full <see cref="Aprs.Services.IGateConfiguration"/> lives at the service layer;
/// this record is the subset the operator controls from the Settings screen.
/// </summary>
public sealed record IGateSettings(
    bool Enabled,
    bool RfToAprsIsGatingEnabled,
    bool AprsIsTransmitEnabled,
    bool GatePositionPackets,
    bool GateWeatherPackets,
    bool GateMessages,
    bool GateObjectItemPackets)
{
    public static IGateSettings Default { get; } = new(
        Enabled:                false,
        RfToAprsIsGatingEnabled: false,
        AprsIsTransmitEnabled:   false,
        GatePositionPackets:     true,
        GateWeatherPackets:      true,
        GateMessages:            true,
        GateObjectItemPackets:   true);
}
