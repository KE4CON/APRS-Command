namespace Aprs.Services;

public sealed record RawPacketLogEntry(
    Guid LogEntryId,
    DateTimeOffset TimestampUtc,
    string RawPacketText,
    string? ParsedPacketType,
    string? SourceCallsign,
    string? Destination,
    IReadOnlyList<string> Path,
    AprsPacketSource PacketSource,
    RawPacketLogDirection Direction,
    string? SourcePortId,
    string? SourcePortName,
    RawPacketValidationStatus ValidationStatus,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    bool ParsedSuccessfully,
    string? RelatedTransmitResult,
    string? Notes);
