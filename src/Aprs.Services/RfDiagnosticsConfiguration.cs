namespace Aprs.Services;

public sealed record RfDiagnosticsConfiguration(
    bool DiagnosticsEnabled,
    TimeSpan DuplicateDetectionWindow,
    TimeSpan PacketRateWindow,
    TimeSpan MinimumBeaconInterval,
    int MaximumRecentPackets,
    int ExcessiveBeaconCountThreshold,
    int ExcessivePortPacketCountThreshold,
    int MaximumRecommendedPathComponents,
    string? Notes)
{
    public static RfDiagnosticsConfiguration Default { get; } = new(
        DiagnosticsEnabled: true,
        DuplicateDetectionWindow: TimeSpan.FromMinutes(2),
        PacketRateWindow: TimeSpan.FromMinutes(10),
        MinimumBeaconInterval: TimeSpan.FromMinutes(1),
        MaximumRecentPackets: 500,
        ExcessiveBeaconCountThreshold: 10,
        ExcessivePortPacketCountThreshold: 100,
        MaximumRecommendedPathComponents: 4,
        Notes: "Receive-only RF diagnostics. This service never gates, digipeats, or transmits packets.");
}
