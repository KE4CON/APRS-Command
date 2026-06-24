namespace Aprs.Services;

public sealed record IGateMonitorConfiguration(
    bool MonitorEnabled,
    bool RfToAprsIsCandidateDetectionEnabled,
    bool AprsIsDuplicateDetectionEnabled,
    IReadOnlyList<string> LocalRfPortsToMonitor,
    string? AprsIsSourceToCompareAgainst,
    TimeSpan DuplicateDetectionWindow,
    TimeSpan CandidateRetentionTime,
    int MaximumCandidateListSize,
    bool RequireValidSourceCallsign,
    bool RequireValidPositionMessageWeatherObjectPacket,
    string? Notes)
{
    public static IGateMonitorConfiguration Default { get; } = new(
        MonitorEnabled: false,
        RfToAprsIsCandidateDetectionEnabled: false,
        AprsIsDuplicateDetectionEnabled: true,
        LocalRfPortsToMonitor: [],
        AprsIsSourceToCompareAgainst: null,
        DuplicateDetectionWindow: TimeSpan.FromMinutes(30),
        CandidateRetentionTime: TimeSpan.FromHours(2),
        MaximumCandidateListSize: 250,
        RequireValidSourceCallsign: true,
        RequireValidPositionMessageWeatherObjectPacket: true,
        Notes: "Monitor-only iGate analysis. RF-to-APRS-IS gating/transmit is not implemented in this task.");
}
