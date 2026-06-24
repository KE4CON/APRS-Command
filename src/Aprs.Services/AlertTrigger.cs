namespace Aprs.Services;

public sealed record AlertTrigger(
    Guid TriggerId,
    Guid RuleId,
    string RuleName,
    DateTimeOffset TimestampUtc,
    AlertSeverity Severity,
    AlertType AlertType,
    string? SourceCallsignOrName,
    string Summary,
    string? Details,
    Guid? RelatedPacketEventOrLogId,
    bool Acknowledged,
    DateTimeOffset? AcknowledgedAtUtc,
    string? Notes);
