namespace Aprs.Services;

public sealed record AlertRule(
    Guid RuleId,
    string RuleName,
    bool Enabled,
    AlertType AlertType,
    AlertConditionType ConditionType,
    string? Target,
    AlertComparisonOperator ComparisonOperator,
    double? ThresholdValue,
    TimeSpan? TimeWindow,
    TimeSpan CooldownInterval,
    AlertSeverity Severity,
    AlertNotificationMethod NotificationMethod,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastTriggeredAtUtc,
    int TriggerCount,
    string? Notes);
