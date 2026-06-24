using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class DigipeaterStatusViewModel
{
    public DigipeaterStatusViewModel(IDigipeaterService digipeaterService)
        : this(digipeaterService.GetStatusSummary(), digipeaterService.GetRecentDecisions())
    {
    }

    public DigipeaterStatusViewModel(DigipeaterStatusSummary summary, IReadOnlyList<DigipeaterDecisionRecord> decisions)
    {
        DigipeaterEnabled = FormatBool(summary.DigipeaterEnabled);
        RfTransmitEnabled = FormatBool(summary.RfTransmitEnabled);
        RfTransmitPort = string.IsNullOrWhiteSpace(summary.RfTransmitPort) ? "-" : summary.RfTransmitPort;
        DigipeaterCallsign = string.IsNullOrWhiteSpace(summary.DigipeaterCallsign) ? "-" : summary.DigipeaterCallsign;
        SupportedAliases = summary.SupportedAliases.Count == 0 ? "-" : string.Join(", ", summary.SupportedAliases);
        FillInMode = FormatBool(summary.FillInDigipeaterMode);
        FullMode = FormatBool(summary.FullDigipeaterMode);
        DecisionSummary = $"{summary.AllowedCount} digipeated, {summary.BlockedCount} blocked, {summary.DuplicateCount} duplicate, {summary.RateLimitedCount} rate limited";
        ErrorSummary = summary.ErrorCount == 0 && summary.InvalidCount == 0
            ? "No digipeater errors recorded."
            : $"{summary.InvalidCount} invalid, {summary.ErrorCount} errors";
        LastDigipeatedPacket = string.IsNullOrWhiteSpace(summary.LastDigipeatedPacket) ? "-" : summary.LastDigipeatedPacket;
        LastBlockedReason = string.IsNullOrWhiteSpace(summary.LastBlockedReason) ? "-" : summary.LastBlockedReason;
        LastDecisionTime = FormatTime(summary.LastDecisionTimestampUtc);
        Decisions = decisions.OrderByDescending(decision => decision.ReceivedTimestampUtc)
            .Select(decision => new DigipeaterDecisionRowViewModel(decision))
            .ToArray();
    }

    public string DigipeaterEnabled { get; }

    public string RfTransmitEnabled { get; }

    public string RfTransmitPort { get; }

    public string DigipeaterCallsign { get; }

    public string SupportedAliases { get; }

    public string FillInMode { get; }

    public string FullMode { get; }

    public string DecisionSummary { get; }

    public string ErrorSummary { get; }

    public string LastDigipeatedPacket { get; }

    public string LastBlockedReason { get; }

    public string LastDecisionTime { get; }

    public IReadOnlyList<DigipeaterDecisionRowViewModel> Decisions { get; }

    public static DigipeaterStatusViewModel CreateDesignTime()
    {
        var summary = new DigipeaterStatusSummary(
            DigipeaterEnabled: false,
            RfTransmitEnabled: false,
            RfTransmitPort: null,
            DigipeaterCallsign: "N0CALL",
            SupportedAliases: ["WIDE1-1", "WIDE2-1"],
            FillInDigipeaterMode: false,
            FullDigipeaterMode: false,
            AllowedCount: 0,
            BlockedCount: 1,
            DuplicateCount: 0,
            RateLimitedCount: 0,
            InvalidCount: 0,
            ErrorCount: 0,
            LastDigipeatedPacket: null,
            LastBlockedReason: "Digipeater mode is disabled.",
            LastDecisionTimestampUtc: DateTimeOffset.UtcNow);

        return new DigipeaterStatusViewModel(summary, []);
    }

    private static string FormatBool(bool value)
    {
        return value ? "Enabled" : "Disabled";
    }

    private static string FormatTime(DateTimeOffset? timestamp)
    {
        return timestamp?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    }
}

public sealed class DigipeaterDecisionRowViewModel
{
    public DigipeaterDecisionRowViewModel(DigipeaterDecisionRecord decision)
    {
        Time = decision.ReceivedTimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Source = string.IsNullOrWhiteSpace(decision.ReceivedRfPort) ? "-" : decision.ReceivedRfPort;
        TransmitPort = string.IsNullOrWhiteSpace(decision.TransmitRfPort) ? "-" : decision.TransmitRfPort;
        Callsign = string.IsNullOrWhiteSpace(decision.SourceCallsign) ? "-" : decision.SourceCallsign;
        PacketType = string.IsNullOrWhiteSpace(decision.ParsedPacketType) ? "Unknown" : decision.ParsedPacketType;
        Decision = decision.Decision.ToString();
        Reason = string.IsNullOrWhiteSpace(decision.Reason) ? "-" : decision.Reason;
        TransmitAttempted = decision.TransmitAttempted ? "Yes" : "No";
        Path = decision.ModifiedPath.Count == 0 ? "-" : string.Join(", ", decision.ModifiedPath);
    }

    public string Time { get; }

    public string Source { get; }

    public string TransmitPort { get; }

    public string Callsign { get; }

    public string PacketType { get; }

    public string Decision { get; }

    public string Reason { get; }

    public string TransmitAttempted { get; }

    public string Path { get; }
}
