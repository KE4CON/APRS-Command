using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class DecodedEventLogRowViewModel
{
    public DecodedEventLogRowViewModel(DecodedEventLogEntry entry)
    {
        TimestampUtc = entry.EventTimestampUtc;
        Timestamp = entry.EventTimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Severity = entry.Severity.ToString();
        Category = entry.EventCategory.ToString();
        EventType = entry.EventType.ToString();
        CallsignOrSource = entry.SourceCallsign ?? entry.RelatedEntity ?? "-";
        Summary = entry.Summary;
        Details = entry.Details ?? string.Empty;
    }

    public DateTimeOffset TimestampUtc { get; }

    public string Timestamp { get; }

    public string Severity { get; }

    public string Category { get; }

    public string EventType { get; }

    public string CallsignOrSource { get; }

    public string Summary { get; }

    public string Details { get; }
}
