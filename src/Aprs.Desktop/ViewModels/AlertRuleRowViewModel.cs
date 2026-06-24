using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class AlertRuleRowViewModel
{
    public AlertRuleRowViewModel(AlertRule rule)
    {
        RuleId = rule.RuleId;
        RuleName = rule.RuleName;
        Enabled = rule.Enabled ? "Enabled" : "Disabled";
        AlertType = rule.AlertType.ToString();
        Target = string.IsNullOrWhiteSpace(rule.Target) ? "Any" : rule.Target;
        Condition = rule.ConditionType.ToString();
        Threshold = rule.ThresholdValue is null ? "-" : $"{rule.ComparisonOperator} {rule.ThresholdValue:0.##}";
        Severity = rule.Severity.ToString();
        LastTriggered = rule.LastTriggeredAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        TriggerCount = rule.TriggerCount.ToString();
    }

    public Guid RuleId { get; }

    public string RuleName { get; }

    public string Enabled { get; }

    public string AlertType { get; }

    public string Target { get; }

    public string Condition { get; }

    public string Threshold { get; }

    public string Severity { get; }

    public string LastTriggered { get; }

    public string TriggerCount { get; }
}
