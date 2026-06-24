using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class EventMonitorRowViewModel
{
    public EventMonitorRowViewModel(IAprsEvent aprsEvent)
    {
        var metadata = aprsEvent.Metadata;
        TimestampUtc = metadata.TimestampUtc;
        Timestamp = metadata.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Category = metadata.EventCategory.ToString();
        EventType = metadata.EventType.ToString();
        Severity = metadata.Severity.ToString();
        Source = metadata.SourceMetadata.SourceName
            ?? metadata.SourceMetadata.SourceType.ToString();
        Summary = metadata.Summary ?? "-";
        Related = metadata.RelatedCallsign
            ?? metadata.RelatedObjectName
            ?? metadata.RelatedMessageId
            ?? metadata.RelatedPacketId
            ?? "-";
    }

    public DateTimeOffset TimestampUtc { get; }

    public string Timestamp { get; }

    public string Category { get; }

    public string EventType { get; }

    public string Severity { get; }

    public string Source { get; }

    public string Summary { get; }

    public string Related { get; }
}
