namespace Aprs.Services;

public sealed record DigipeaterStatusSummary(
    bool DigipeaterEnabled,
    bool RfTransmitEnabled,
    string? RfTransmitPort,
    string DigipeaterCallsign,
    IReadOnlyList<string> SupportedAliases,
    bool FillInDigipeaterMode,
    bool FullDigipeaterMode,
    int AllowedCount,
    int BlockedCount,
    int DuplicateCount,
    int RateLimitedCount,
    int InvalidCount,
    int ErrorCount,
    string? LastDigipeatedPacket,
    string? LastBlockedReason,
    DateTimeOffset? LastDecisionTimestampUtc);
