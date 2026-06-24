namespace Aprs.Services;

public sealed record IGateMonitorSummary(
    bool MonitorEnabled,
    int TotalCandidates,
    int CandidateCount,
    int RejectedCount,
    int DuplicateCount,
    int AlreadySeenOnAprsIsCount,
    int InvalidCount,
    int ExpiredCount,
    DateTimeOffset? LastRfPacketUtc,
    DateTimeOffset? LastAprsIsPacketUtc,
    string? LastReason);
