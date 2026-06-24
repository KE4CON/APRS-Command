using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class IGateStatusViewModel
{
    public IGateStatusViewModel(IIGateService iGateService)
        : this(iGateService.GetStatusSummary(), iGateService.GetRecentDecisions())
    {
    }

    public IGateStatusViewModel(IGateStatusSummary summary, IReadOnlyList<IGateGatingDecisionRecord> decisions)
    {
        IGateEnabled = FormatBool(summary.IGateEnabled);
        RfToAprsIsGatingEnabled = FormatBool(summary.RfToAprsIsGatingEnabled);
        AprsIsTransmitEnabled = FormatBool(summary.AprsIsTransmitEnabled);
        AprsIsTransmitRequired = FormatBool(summary.AprsIsTransmitRequired);
        DecisionSummary = $"{summary.AllowedCount} gated, {summary.BlockedCount} blocked, {summary.DuplicateCount} duplicate, {summary.RateLimitedCount} rate limited";
        ErrorSummary = summary.ErrorCount == 0 && summary.InvalidCount == 0
            ? "No iGate errors recorded."
            : $"{summary.InvalidCount} invalid, {summary.ErrorCount} errors";
        LastGatedPacket = string.IsNullOrWhiteSpace(summary.LastGatedPacket) ? "-" : summary.LastGatedPacket;
        LastBlockedReason = string.IsNullOrWhiteSpace(summary.LastBlockedReason) ? "-" : summary.LastBlockedReason;
        LastDecisionTime = FormatTime(summary.LastDecisionTimestampUtc);
        Decisions = decisions.OrderByDescending(decision => decision.ReceivedTimestampUtc)
            .Select(decision => new IGateDecisionRowViewModel(decision))
            .ToArray();
    }

    public string IGateEnabled { get; }

    public string RfToAprsIsGatingEnabled { get; }

    public string AprsIsTransmitEnabled { get; }

    public string AprsIsTransmitRequired { get; }

    public string DecisionSummary { get; }

    public string ErrorSummary { get; }

    public string LastGatedPacket { get; }

    public string LastBlockedReason { get; }

    public string LastDecisionTime { get; }

    public IReadOnlyList<IGateDecisionRowViewModel> Decisions { get; }

    public static IGateStatusViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var summary = new IGateStatusSummary(
            IGateEnabled: false,
            RfToAprsIsGatingEnabled: false,
            AprsIsTransmitEnabled: false,
            AprsIsTransmitRequired: true,
            AllowedCount: 0,
            BlockedCount: 1,
            DuplicateCount: 0,
            RateLimitedCount: 0,
            InvalidCount: 0,
            ErrorCount: 0,
            LastGatedPacket: null,
            LastBlockedReason: "iGate is disabled.",
            LastDecisionTimestampUtc: now);

        return new IGateStatusViewModel(summary, []);
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

public sealed class IGateDecisionRowViewModel
{
    public IGateDecisionRowViewModel(IGateGatingDecisionRecord decision)
    {
        Time = decision.ReceivedTimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Source = string.IsNullOrWhiteSpace(decision.ReceivedRfPort) ? "-" : decision.ReceivedRfPort;
        Callsign = string.IsNullOrWhiteSpace(decision.SourceCallsign) ? "-" : decision.SourceCallsign;
        PacketType = string.IsNullOrWhiteSpace(decision.ParsedPacketType) ? "Unknown" : decision.ParsedPacketType;
        Decision = decision.Decision.ToString();
        Reason = string.IsNullOrWhiteSpace(decision.Reason) ? "-" : decision.Reason;
        TransmitAttempted = decision.TransmitAttempted ? "Yes" : "No";
    }

    public string Time { get; }

    public string Source { get; }

    public string Callsign { get; }

    public string PacketType { get; }

    public string Decision { get; }

    public string Reason { get; }

    public string TransmitAttempted { get; }
}
