using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class AlertTriggerRowViewModel
{
    public AlertTriggerRowViewModel(AlertTrigger trigger)
    {
        TriggerId = trigger.TriggerId;
        Time = trigger.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        Severity = trigger.Severity.ToString();
        AlertType = trigger.AlertType.ToString();
        Source = string.IsNullOrWhiteSpace(trigger.SourceCallsignOrName) ? "-" : trigger.SourceCallsignOrName;
        Summary = trigger.Summary;
        Details = string.IsNullOrWhiteSpace(trigger.Details) ? "-" : trigger.Details;
        Acknowledged = trigger.Acknowledged ? "Yes" : "No";
    }

    public Guid TriggerId { get; }

    public string Time { get; }

    public string Severity { get; }

    public string AlertType { get; }

    public string Source { get; }

    public string Summary { get; }

    public string Details { get; }

    public string Acknowledged { get; }
}
