namespace Aprs.Services;

public sealed record ReplayLogEntry(
    Guid ReplayEntryId,
    DateTimeOffset OriginalTimestampUtc,
    DateTimeOffset? ReplayTimestampUtc,
    string RawPacketText,
    AprsPacketSource PacketSource,
    AprsPacketSource OriginalPacketSource,
    RawPacketLogDirection Direction,
    string? ParsedPacketType,
    string? SourceCallsign,
    string? Destination,
    IReadOnlyList<string> Path,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    string? Notes);
