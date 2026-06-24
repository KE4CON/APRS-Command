using System.Text.RegularExpressions;
using AprsCommand.Contracts;

namespace Aprs.Services;

public sealed partial class DecodedEventLogService : IDecodedEventLogService
{
    private readonly IBeaconSchedulerClock clock;
    private readonly DecodedEventLogConfiguration configuration;
    private readonly IAprsEventBus? eventBus;
    private readonly List<DecodedEventLogEntry> entries = [];

    public DecodedEventLogService(
        DecodedEventLogConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null,
        IAprsEventBus? eventBus = null)
    {
        this.configuration = configuration ?? DecodedEventLogConfiguration.Default;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.eventBus = eventBus;
    }

    public DecodedEventLogEntry? AddEvent(
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
        DateTimeOffset? timestampUtc = null)
    {
        if (!configuration.DecodedEventLoggingEnabled)
        {
            return null;
        }

        var sanitizedData = (structuredEventData ?? new Dictionary<string, string>())
            .ToDictionary(pair => SanitizeRequired(pair.Key), pair => SanitizeRequired(pair.Value), StringComparer.OrdinalIgnoreCase);

        var entry = new DecodedEventLogEntry(
            Guid.NewGuid(),
            timestampUtc ?? clock.UtcNow,
            eventType,
            eventCategory,
            severity,
            Sanitize(sourceCallsign),
            Sanitize(relatedEntity),
            packetSource,
            relatedRawPacketLogEntryId,
            SanitizeRequired(summary),
            Sanitize(details),
            sanitizedData,
            (validationErrors ?? []).Select(SanitizeRequired).ToArray(),
            (validationWarnings ?? []).Select(SanitizeRequired).ToArray(),
            Sanitize(notes));

        entries.Add(entry);
        TrimEntries();
        PublishAprsEvent(entry);
        return entry;
    }

    public DecodedEventLogEntry? AddStationEvent(DecodedEventType eventType, string callsign, string summary, AprsPacketSource? packetSource = null, string? details = null)
    {
        return AddEvent(eventType, DecodedEventCategory.Station, DecodedEventSeverity.Info, summary, details, callsign, callsign, packetSource);
    }

    public DecodedEventLogEntry? AddObjectEvent(DecodedEventType eventType, string objectName, string summary, AprsPacketSource? packetSource = null, string? ownerCallsign = null)
    {
        var severity = eventType == DecodedEventType.ObjectKilled ? DecodedEventSeverity.Warning : DecodedEventSeverity.Info;
        return AddEvent(eventType, DecodedEventCategory.Object, severity, summary, relatedEntity: objectName, sourceCallsign: ownerCallsign, packetSource: packetSource);
    }

    public DecodedEventLogEntry? AddWeatherEvent(string sourceName, string summary, AprsPacketSource? packetSource = null, string? details = null)
    {
        return AddEvent(DecodedEventType.WeatherUpdated, DecodedEventCategory.Weather, DecodedEventSeverity.Info, summary, details, relatedEntity: sourceName, packetSource: packetSource);
    }

    public DecodedEventLogEntry? AddMessageEvent(DecodedEventType eventType, string remoteCallsign, string summary, AprsPacketSource? packetSource = null, string? details = null)
    {
        var severity = eventType == DecodedEventType.MessageRejected ? DecodedEventSeverity.Warning : DecodedEventSeverity.Info;
        return AddEvent(eventType, DecodedEventCategory.Message, severity, summary, details, remoteCallsign, remoteCallsign, packetSource);
    }

    public DecodedEventLogEntry? AddGpsEvent(string sourceName, string summary, DecodedEventSeverity severity = DecodedEventSeverity.Info, string? details = null)
    {
        return AddEvent(DecodedEventType.GpsUpdated, DecodedEventCategory.GPS, severity, summary, details, relatedEntity: sourceName);
    }

    public DecodedEventLogEntry? AddPortEvent(DecodedEventType eventType, string portName, string summary, DecodedEventSeverity severity = DecodedEventSeverity.Info, string? details = null)
    {
        var category = eventType is DecodedEventType.AprsIsConnected or DecodedEventType.AprsIsDisconnected
            ? DecodedEventCategory.AprsIs
            : DecodedEventCategory.Port;
        return AddEvent(eventType, category, severity, summary, details, relatedEntity: portName);
    }

    public DecodedEventLogEntry? AddTransmitEvent(DecodedEventType eventType, string summary, AprsPacketSource? packetSource = null, string? sourceCallsign = null, string? details = null)
    {
        var severity = eventType == DecodedEventType.PacketTransmitBlocked ? DecodedEventSeverity.Warning : DecodedEventSeverity.Info;
        return AddEvent(eventType, DecodedEventCategory.Packet, severity, summary, details, sourceCallsign, sourceCallsign, packetSource);
    }

    public DecodedEventLogEntry? AddIGateEvent(IGateGatingDecisionRecord decision)
    {
        var gated = decision.Decision == IGateDecision.Allowed;
        return AddEvent(
            gated ? DecodedEventType.IGatePacketGated : DecodedEventType.IGatePacketBlocked,
            DecodedEventCategory.IGate,
            gated ? DecodedEventSeverity.Info : DecodedEventSeverity.Warning,
            gated ? "RF packet gated to APRS-IS." : "iGate candidate blocked.",
            decision.Reason,
            decision.SourceCallsign,
            decision.ReceivedRfPort,
            AprsPacketSource.Rf,
            structuredEventData: new Dictionary<string, string>
            {
                ["Decision"] = decision.Decision.ToString(),
                ["PacketType"] = decision.ParsedPacketType,
                ["TransmitAttempted"] = decision.TransmitAttempted.ToString()
            },
            validationErrors: decision.ValidationErrors,
            validationWarnings: decision.ValidationWarnings);
    }

    public DecodedEventLogEntry? AddDigipeaterEvent(DigipeaterDecisionRecord decision)
    {
        var repeated = decision.Decision == DigipeaterDecision.Allowed;
        return AddEvent(
            repeated ? DecodedEventType.DigipeaterPacketRepeated : DecodedEventType.DigipeaterPacketBlocked,
            DecodedEventCategory.Digipeater,
            repeated ? DecodedEventSeverity.Info : DecodedEventSeverity.Warning,
            repeated ? "RF packet digipeated." : "Digipeater packet blocked.",
            decision.Reason,
            decision.SourceCallsign,
            decision.TransmitRfPort ?? decision.ReceivedRfPort,
            AprsPacketSource.Rf,
            structuredEventData: new Dictionary<string, string>
            {
                ["Decision"] = decision.Decision.ToString(),
                ["PacketType"] = decision.ParsedPacketType,
                ["TransmitAttempted"] = decision.TransmitAttempted.ToString()
            },
            validationErrors: decision.ValidationErrors,
            validationWarnings: decision.ValidationWarnings);
    }

    public IReadOnlyList<DecodedEventLogEntry> GetRecentEvents(int? maximumCount = null)
    {
        var query = entries.OrderByDescending(entry => entry.EventTimestampUtc).ThenByDescending(entry => entry.EventId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    public IReadOnlyList<DecodedEventLogEntry> GetEventsByType(DecodedEventType eventType)
    {
        return GetRecentEvents().Where(entry => entry.EventType == eventType).ToArray();
    }

    public IReadOnlyList<DecodedEventLogEntry> GetEventsByCategory(DecodedEventCategory category)
    {
        return GetRecentEvents().Where(entry => entry.EventCategory == category).ToArray();
    }

    public IReadOnlyList<DecodedEventLogEntry> GetEventsBySeverity(DecodedEventSeverity severity)
    {
        return GetRecentEvents().Where(entry => entry.Severity == severity).ToArray();
    }

    public IReadOnlyList<DecodedEventLogEntry> GetEventsByCallsignOrSource(string callsignOrSource)
    {
        if (string.IsNullOrWhiteSpace(callsignOrSource))
        {
            return [];
        }

        var query = callsignOrSource.Trim();
        return GetRecentEvents()
            .Where(entry =>
                string.Equals(entry.SourceCallsign, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.RelatedEntity, query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<DecodedEventLogEntry> SearchEvents(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return GetRecentEvents();
        }

        var query = searchText.Trim();
        return GetRecentEvents()
            .Where(entry =>
                entry.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (entry.Details?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Notes?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || entry.StructuredEventData.Any(pair =>
                    pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || pair.Value.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public void ClearEventLog()
    {
        entries.Clear();
    }

    private void TrimEntries()
    {
        var maximum = Math.Max(0, configuration.MaximumInMemoryEvents);
        if (maximum == 0)
        {
            entries.Clear();
            return;
        }

        while (entries.Count > maximum)
        {
            var oldest = entries.OrderBy(entry => entry.EventTimestampUtc).ThenBy(entry => entry.EventId).First();
            entries.Remove(oldest);
        }
    }

    private static string? Sanitize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var sanitized = AprsIsPassRegex().Replace(value, "$1[REDACTED]");
        sanitized = CredentialKeyValueRegex().Replace(sanitized, "$1[REDACTED]");
        return sanitized;
    }

    private static string SanitizeRequired(string value)
    {
        return Sanitize(value) ?? string.Empty;
    }

    [GeneratedRegex(@"(?i)\b(pass\s+)(\S+)")]
    private static partial Regex AprsIsPassRegex();

    [GeneratedRegex(@"(?i)\b((?:api[_-]?key|token|password|passcode|secret)\s*[:=]\s*)([^\s,;]+)")]
    private static partial Regex CredentialKeyValueRegex();

    private void PublishAprsEvent(DecodedEventLogEntry entry)
    {
        if (eventBus is null)
        {
            return;
        }

        var metadata = new AprsEventMetadata(
            entry.EventId,
            MapEventType(entry.EventType),
            MapEventCategory(entry.EventCategory),
            entry.EventTimestampUtc,
            CreateSourceMetadata(entry),
            MapSeverity(entry.Severity),
            RelatedCallsign: entry.SourceCallsign,
            RelatedObjectName: entry.EventCategory == DecodedEventCategory.Object ? entry.RelatedEntity : null,
            RelatedMessageId: entry.EventCategory is DecodedEventCategory.Message or DecodedEventCategory.Bulletin ? entry.RelatedEntity : null,
            RelatedPacketId: entry.RelatedRawPacketLogEntryId?.ToString(),
            Summary: entry.Summary,
            Notes: entry.Notes);

        var attributes = entry.StructuredEventData
            .Concat(new[]
            {
                new KeyValuePair<string, string>("DecodedEventCategory", entry.EventCategory.ToString()),
                new KeyValuePair<string, string>("DecodedEventSeverity", entry.Severity.ToString())
            })
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        eventBus.Publish(new AprsEventEnvelope<DecodedEventLogEntry>(metadata, entry, attributes));
    }

    private static ExternalSourceMetadata CreateSourceMetadata(DecodedEventLogEntry entry)
    {
        var sourceType = entry.PacketSource is null
            ? ExternalSourceType.Unknown
            : MapPacketSource(entry.PacketSource.Value);

        return new ExternalSourceMetadata(
            SourceName: entry.RelatedEntity ?? entry.SourceCallsign,
            SourceType: sourceType,
            SourceId: entry.RelatedEntity ?? entry.SourceCallsign,
            Timestamp: entry.EventTimestampUtc,
            Origin: ContractDataOrigin.Generated,
            TrustLevel: ExternalTrustLevel.Internal);
    }

    private static ExternalSourceType MapPacketSource(AprsPacketSource packetSource)
    {
        return packetSource switch
        {
            AprsPacketSource.AprsIs => ExternalSourceType.AprsIs,
            AprsPacketSource.Rf => ExternalSourceType.Rf,
            AprsPacketSource.TcpKiss => ExternalSourceType.TcpKiss,
            AprsPacketSource.SerialKiss => ExternalSourceType.SerialKiss,
            AprsPacketSource.Direwolf => ExternalSourceType.Direwolf,
            AprsPacketSource.Agwpe => ExternalSourceType.Agwpe,
            AprsPacketSource.Replay => ExternalSourceType.Replay,
            AprsPacketSource.Simulation => ExternalSourceType.Simulation,
            AprsPacketSource.External => ExternalSourceType.Plugin,
            AprsPacketSource.LocalGenerated => ExternalSourceType.LocalApi,
            _ => ExternalSourceType.Unknown
        };
    }

    private static AprsEventCategory MapEventCategory(DecodedEventCategory category)
    {
        return category switch
        {
            DecodedEventCategory.Packet => AprsEventCategory.Packet,
            DecodedEventCategory.Station => AprsEventCategory.Station,
            DecodedEventCategory.Object => AprsEventCategory.Object,
            DecodedEventCategory.Weather => AprsEventCategory.Weather,
            DecodedEventCategory.Message => AprsEventCategory.Message,
            DecodedEventCategory.Bulletin => AprsEventCategory.Message,
            DecodedEventCategory.GPS => AprsEventCategory.GPS,
            DecodedEventCategory.Port => AprsEventCategory.Port,
            DecodedEventCategory.AprsIs => AprsEventCategory.AprsIs,
            DecodedEventCategory.RF => AprsEventCategory.RF,
            DecodedEventCategory.Beacon => AprsEventCategory.Beacon,
            DecodedEventCategory.IGate => AprsEventCategory.IGate,
            DecodedEventCategory.Digipeater => AprsEventCategory.Digipeater,
            DecodedEventCategory.Alert => AprsEventCategory.Alert,
            _ => AprsEventCategory.System
        };
    }

    private static AprsEventSeverity MapSeverity(DecodedEventSeverity severity)
    {
        return severity switch
        {
            DecodedEventSeverity.Debug => AprsEventSeverity.Trace,
            DecodedEventSeverity.Warning => AprsEventSeverity.Warning,
            DecodedEventSeverity.Error => AprsEventSeverity.Error,
            DecodedEventSeverity.Critical => AprsEventSeverity.Critical,
            _ => AprsEventSeverity.Info
        };
    }

    private static AprsEventType MapEventType(DecodedEventType eventType)
    {
        return eventType switch
        {
            DecodedEventType.StationCreated => AprsEventType.StationCreated,
            DecodedEventType.StationUpdated => AprsEventType.StationUpdated,
            DecodedEventType.StationExpired => AprsEventType.StationExpired,
            DecodedEventType.ObjectCreated => AprsEventType.ObjectCreated,
            DecodedEventType.ObjectUpdated => AprsEventType.ObjectUpdated,
            DecodedEventType.ObjectKilled => AprsEventType.ObjectKilled,
            DecodedEventType.WeatherUpdated => AprsEventType.WeatherUpdated,
            DecodedEventType.MessageReceived => AprsEventType.MessageReceived,
            DecodedEventType.MessageSent => AprsEventType.MessageSent,
            DecodedEventType.MessageAcknowledged => AprsEventType.MessageAcknowledged,
            DecodedEventType.MessageRejected => AprsEventType.MessageRejected,
            DecodedEventType.BulletinReceived => AprsEventType.BulletinReceived,
            DecodedEventType.GpsUpdated => AprsEventType.GpsUpdated,
            DecodedEventType.PortConnected => AprsEventType.PortConnected,
            DecodedEventType.PortDisconnected => AprsEventType.PortDisconnected,
            DecodedEventType.AprsIsConnected => AprsEventType.AprsIsConnected,
            DecodedEventType.AprsIsDisconnected => AprsEventType.AprsIsDisconnected,
            DecodedEventType.BeaconGenerated => AprsEventType.BeaconGenerated,
            DecodedEventType.BeaconTransmitted => AprsEventType.BeaconTransmitted,
            DecodedEventType.WeatherBeaconGenerated => AprsEventType.WeatherBeaconGenerated,
            DecodedEventType.WeatherBeaconTransmitted => AprsEventType.WeatherBeaconTransmitted,
            DecodedEventType.PacketTransmitBlocked => AprsEventType.PacketTransmitBlocked,
            DecodedEventType.PacketTransmitted => AprsEventType.PacketTransmitted,
            DecodedEventType.IGateCandidateDetected => AprsEventType.IGateCandidateDetected,
            DecodedEventType.IGatePacketGated => AprsEventType.IGatePacketGated,
            DecodedEventType.IGatePacketBlocked => AprsEventType.IGatePacketBlocked,
            DecodedEventType.DigipeaterPacketRepeated => AprsEventType.DigipeaterPacketRepeated,
            DecodedEventType.DigipeaterPacketBlocked => AprsEventType.DigipeaterPacketBlocked,
            DecodedEventType.AlertTriggered => AprsEventType.AlertTriggered,
            DecodedEventType.TrainingScenarioUpdated => AprsEventType.TrainingStateChanged,
            _ => AprsEventType.ExtensionEvent
        };
    }
}
