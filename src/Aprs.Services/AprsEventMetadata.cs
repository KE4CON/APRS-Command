using AprsCommand.Contracts;

namespace Aprs.Services;

public sealed record AprsEventMetadata(
    Guid EventId,
    AprsEventType EventType,
    AprsEventCategory EventCategory,
    DateTimeOffset TimestampUtc,
    ExternalSourceMetadata SourceMetadata,
    AprsEventSeverity Severity = AprsEventSeverity.Info,
    string? RelatedCallsign = null,
    string? RelatedObjectName = null,
    string? RelatedMessageId = null,
    string? RelatedPacketId = null,
    string? Summary = null,
    string? Notes = null)
{
    public static AprsEventMetadata Create(
        AprsEventType eventType,
        AprsEventCategory eventCategory,
        DateTimeOffset timestampUtc,
        ExternalSourceMetadata? sourceMetadata = null,
        AprsEventSeverity severity = AprsEventSeverity.Info,
        string? relatedCallsign = null,
        string? relatedObjectName = null,
        string? relatedMessageId = null,
        string? relatedPacketId = null,
        string? summary = null,
        string? notes = null)
    {
        return new AprsEventMetadata(
            Guid.NewGuid(),
            eventType,
            eventCategory,
            timestampUtc,
            sourceMetadata ?? new ExternalSourceMetadata(Timestamp: timestampUtc),
            severity,
            relatedCallsign,
            relatedObjectName,
            relatedMessageId,
            relatedPacketId,
            summary,
            notes);
    }
}
