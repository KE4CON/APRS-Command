using System.Text.RegularExpressions;
using Aprs.Core;

namespace Aprs.Services;

public sealed partial class RawPacketLogService : IRawPacketLogService
{
    private readonly IAprsParser parser;
    private readonly IBeaconSchedulerClock clock;
    private readonly RawPacketLogConfiguration configuration;
    private readonly List<RawPacketLogEntry> entries = [];

    public RawPacketLogService(
        IAprsParser? parser = null,
        RawPacketLogConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null)
    {
        this.parser = parser ?? new AprsParser();
        this.configuration = configuration ?? RawPacketLogConfiguration.Default;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
    }

    public RawPacketLogEntry? AddReceivedRawPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? notes = null)
    {
        return AddRawPacket(rawPacketText, packetSource, RawPacketLogDirection.Received, sourcePortId, sourcePortName, timestampUtc, null, notes);
    }

    public RawPacketLogEntry? AddReceivedPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        string? notes = null)
    {
        return AddPacket(packet, packetSource, RawPacketLogDirection.Received, sourcePortId, sourcePortName, null, notes);
    }

    public RawPacketLogEntry? AddTransmittedPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? relatedTransmitResult = null,
        string? notes = null)
    {
        return AddRawPacket(rawPacketText, packetSource, RawPacketLogDirection.Transmitted, sourcePortId, sourcePortName, timestampUtc, relatedTransmitResult, notes);
    }

    public RawPacketLogEntry? AddGeneratedPacket(
        string rawPacketText,
        AprsPacketSource packetSource = AprsPacketSource.LocalGenerated,
        DateTimeOffset? timestampUtc = null,
        string? notes = null)
    {
        return AddRawPacket(rawPacketText, packetSource, RawPacketLogDirection.Generated, null, null, timestampUtc, null, notes);
    }

    public RawPacketLogEntry? AddBlockedPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? relatedTransmitResult = null,
        string? notes = null)
    {
        return AddRawPacket(rawPacketText, packetSource, RawPacketLogDirection.Blocked, sourcePortId, sourcePortName, timestampUtc, relatedTransmitResult, notes);
    }

    public IReadOnlyList<RawPacketLogEntry> GetRecentEntries(int? maximumCount = null)
    {
        var query = entries.OrderByDescending(entry => entry.TimestampUtc).ThenByDescending(entry => entry.LogEntryId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    public IReadOnlyList<RawPacketLogEntry> GetEntriesBySourceCallsign(string sourceCallsign)
    {
        if (string.IsNullOrWhiteSpace(sourceCallsign))
        {
            return [];
        }

        return GetRecentEntries()
            .Where(entry => string.Equals(entry.SourceCallsign, sourceCallsign.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<RawPacketLogEntry> GetEntriesByPacketSource(AprsPacketSource packetSource)
    {
        return GetRecentEntries().Where(entry => entry.PacketSource == packetSource).ToArray();
    }

    public IReadOnlyList<RawPacketLogEntry> GetEntriesByDirection(RawPacketLogDirection direction)
    {
        return GetRecentEntries().Where(entry => entry.Direction == direction).ToArray();
    }

    public IReadOnlyList<RawPacketLogEntry> GetEntriesByPacketType(string packetType)
    {
        if (string.IsNullOrWhiteSpace(packetType))
        {
            return [];
        }

        return GetRecentEntries()
            .Where(entry => string.Equals(entry.ParsedPacketType, packetType.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<RawPacketLogEntry> SearchPacketText(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return GetRecentEntries();
        }

        return GetRecentEntries()
            .Where(entry => entry.RawPacketText.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public void ClearLog()
    {
        entries.Clear();
    }

    private RawPacketLogEntry? AddRawPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        RawPacketLogDirection direction,
        string? sourcePortId,
        string? sourcePortName,
        DateTimeOffset? timestampUtc,
        string? relatedTransmitResult,
        string? notes)
    {
        var timestamp = timestampUtc ?? clock.UtcNow;
        var sanitized = SanitizeRequired(rawPacketText);
        var packet = ParsePacket(sanitized, timestamp);
        return AddPacket(packet, packetSource, direction, sourcePortId, sourcePortName, relatedTransmitResult, Sanitize(notes));
    }

    private RawPacketLogEntry? AddPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        RawPacketLogDirection direction,
        string? sourcePortId,
        string? sourcePortName,
        string? relatedTransmitResult,
        string? notes)
    {
        if (!ShouldLog(direction))
        {
            return null;
        }

        var sanitizedRawPacket = SanitizeRequired(packet.RawLine);
        var sanitizedNotes = Sanitize(notes);
        var sanitizedResult = Sanitize(relatedTransmitResult);
        var reparsed = string.Equals(sanitizedRawPacket, packet.RawLine, StringComparison.Ordinal)
            ? packet
            : ParsePacket(sanitizedRawPacket, packet.ReceivedAtUtc);

        var entry = new RawPacketLogEntry(
            Guid.NewGuid(),
            reparsed.ReceivedAtUtc,
            sanitizedRawPacket,
            GetPacketType(reparsed),
            string.IsNullOrWhiteSpace(reparsed.SourceCallsign) ? null : reparsed.SourceCallsign,
            string.IsNullOrWhiteSpace(reparsed.Destination) ? null : reparsed.Destination,
            reparsed.Path,
            packetSource,
            direction,
            Sanitize(sourcePortId),
            Sanitize(sourcePortName),
            reparsed.IsValid ? RawPacketValidationStatus.Valid : RawPacketValidationStatus.Invalid,
            reparsed.ValidationErrors,
            [],
            reparsed.IsValid,
            sanitizedResult,
            sanitizedNotes);

        entries.Add(entry);
        TrimEntries();
        return entry;
    }

    private bool ShouldLog(RawPacketLogDirection direction)
    {
        if (!configuration.RawPacketLoggingEnabled)
        {
            return false;
        }

        return direction switch
        {
            RawPacketLogDirection.Received => configuration.IncludeReceivedPackets,
            RawPacketLogDirection.Transmitted => configuration.IncludeTransmittedPackets,
            RawPacketLogDirection.Blocked => configuration.IncludeBlockedTransmitAttempts,
            RawPacketLogDirection.Generated => configuration.IncludeGeneratedPackets,
            _ => true
        };
    }

    private void TrimEntries()
    {
        var maximum = Math.Max(0, configuration.MaximumInMemoryEntries);
        if (maximum == 0)
        {
            entries.Clear();
            return;
        }

        while (entries.Count > maximum)
        {
            var oldest = entries.OrderBy(entry => entry.TimestampUtc).ThenBy(entry => entry.LogEntryId).First();
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

    private AprsPacket ParsePacket(string rawPacketText, DateTimeOffset timestampUtc)
    {
        if (parser.TryParse(rawPacketText, timestampUtc, out var packet, out _)
            && packet is not null)
        {
            return packet;
        }

        return packet ?? new AprsParser().Parse(rawPacketText, timestampUtc);
    }

    private static string GetPacketType(AprsPacket packet)
    {
        var typeName = packet.GetType().Name;
        return typeName.EndsWith("AprsPacket", StringComparison.Ordinal)
            ? typeName[..^"AprsPacket".Length]
            : typeName;
    }

    [GeneratedRegex(@"(?i)\b(pass\s+)(\S+)")]
    private static partial Regex AprsIsPassRegex();

    [GeneratedRegex(@"(?i)\b((?:api[_-]?key|token|password|passcode|secret)\s*[:=]\s*)([^\s,;]+)")]
    private static partial Regex CredentialKeyValueRegex();
}
