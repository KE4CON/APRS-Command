using Aprs.Core;

namespace Aprs.Services;

public interface IRawPacketLogService
{
    RawPacketLogEntry? AddReceivedRawPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? notes = null);

    RawPacketLogEntry? AddReceivedPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        string? notes = null);

    RawPacketLogEntry? AddTransmittedPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? relatedTransmitResult = null,
        string? notes = null);

    RawPacketLogEntry? AddGeneratedPacket(
        string rawPacketText,
        AprsPacketSource packetSource = AprsPacketSource.LocalGenerated,
        DateTimeOffset? timestampUtc = null,
        string? notes = null);

    RawPacketLogEntry? AddBlockedPacket(
        string rawPacketText,
        AprsPacketSource packetSource,
        string? sourcePortId = null,
        string? sourcePortName = null,
        DateTimeOffset? timestampUtc = null,
        string? relatedTransmitResult = null,
        string? notes = null);

    IReadOnlyList<RawPacketLogEntry> GetRecentEntries(int? maximumCount = null);

    IReadOnlyList<RawPacketLogEntry> GetEntriesBySourceCallsign(string sourceCallsign);

    IReadOnlyList<RawPacketLogEntry> GetEntriesByPacketSource(AprsPacketSource packetSource);

    IReadOnlyList<RawPacketLogEntry> GetEntriesByDirection(RawPacketLogDirection direction);

    IReadOnlyList<RawPacketLogEntry> GetEntriesByPacketType(string packetType);

    IReadOnlyList<RawPacketLogEntry> SearchPacketText(string searchText);

    void ClearLog();
}
