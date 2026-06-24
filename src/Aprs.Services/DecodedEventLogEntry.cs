namespace Aprs.Services;

public sealed record DecodedEventLogEntry(
    Guid EventId,
    DateTimeOffset EventTimestampUtc,
    DecodedEventType EventType,
    DecodedEventCategory EventCategory,
    DecodedEventSeverity Severity,
    string? SourceCallsign,
    string? RelatedEntity,
    AprsPacketSource? PacketSource,
    Guid? RelatedRawPacketLogEntryId,
    string Summary,
    string? Details,
    IReadOnlyDictionary<string, string> StructuredEventData,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    string? Notes);
