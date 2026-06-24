namespace Aprs.Services;

public interface IDecodedEventLogService
{
    DecodedEventLogEntry? AddEvent(
        DecodedEventType eventType,
        DecodedEventCategory eventCategory,
        DecodedEventSeverity severity,
        string summary,
        string? details = null,
        string? sourceCallsign = null,
        string? relatedEntity = null,
        AprsPacketSource? packetSource = null,
        Guid? relatedRawPacketLogEntryId = null,
        IReadOnlyDictionary<string, string>? structuredEventData = null,
        IReadOnlyList<string>? validationErrors = null,
        IReadOnlyList<string>? validationWarnings = null,
        string? notes = null,
        DateTimeOffset? timestampUtc = null);

    DecodedEventLogEntry? AddStationEvent(DecodedEventType eventType, string callsign, string summary, AprsPacketSource? packetSource = null, string? details = null);

    DecodedEventLogEntry? AddObjectEvent(DecodedEventType eventType, string objectName, string summary, AprsPacketSource? packetSource = null, string? ownerCallsign = null);

    DecodedEventLogEntry? AddWeatherEvent(string sourceName, string summary, AprsPacketSource? packetSource = null, string? details = null);

    DecodedEventLogEntry? AddMessageEvent(DecodedEventType eventType, string remoteCallsign, string summary, AprsPacketSource? packetSource = null, string? details = null);

    DecodedEventLogEntry? AddGpsEvent(string sourceName, string summary, DecodedEventSeverity severity = DecodedEventSeverity.Info, string? details = null);

    DecodedEventLogEntry? AddPortEvent(DecodedEventType eventType, string portName, string summary, DecodedEventSeverity severity = DecodedEventSeverity.Info, string? details = null);

    DecodedEventLogEntry? AddTransmitEvent(DecodedEventType eventType, string summary, AprsPacketSource? packetSource = null, string? sourceCallsign = null, string? details = null);

    DecodedEventLogEntry? AddIGateEvent(IGateGatingDecisionRecord decision);

    DecodedEventLogEntry? AddDigipeaterEvent(DigipeaterDecisionRecord decision);

    IReadOnlyList<DecodedEventLogEntry> GetRecentEvents(int? maximumCount = null);

    IReadOnlyList<DecodedEventLogEntry> GetEventsByType(DecodedEventType eventType);

    IReadOnlyList<DecodedEventLogEntry> GetEventsByCategory(DecodedEventCategory category);

    IReadOnlyList<DecodedEventLogEntry> GetEventsBySeverity(DecodedEventSeverity severity);

    IReadOnlyList<DecodedEventLogEntry> GetEventsByCallsignOrSource(string callsignOrSource);

    IReadOnlyList<DecodedEventLogEntry> SearchEvents(string searchText);

    void ClearEventLog();
}
