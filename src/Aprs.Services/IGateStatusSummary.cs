namespace Aprs.Services;

public sealed record IGateStatusSummary(
    bool IGateEnabled,
    bool RfToAprsIsGatingEnabled,
    bool AprsIsTransmitEnabled,
    bool AprsIsTransmitRequired,
    int AllowedCount,
    int BlockedCount,
    int DuplicateCount,
    int RateLimitedCount,
    int InvalidCount,
    int ErrorCount,
    string? LastGatedPacket,
    string? LastBlockedReason,
    DateTimeOffset? LastDecisionTimestampUtc);
