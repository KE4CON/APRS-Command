namespace Aprs.Services;

public sealed record RfDiagnosticPacket(
    Guid DiagnosticId,
    string RawPacket,
    string ParsedPacketType,
    string SourceCallsign,
    string Destination,
    IReadOnlyList<string> Path,
    AprsPacketSource PacketSource,
    DateTimeOffset ReceivedTimestampUtc,
    string ReceivedPortOrSource,
    bool IsReceivedFromRf,
    bool WasAlsoSeenOnAprsIs,
    RfDiagnosticDuplicateState DuplicateState,
    int DuplicateCount,
    DateTimeOffset FirstSeenTimestampUtc,
    DateTimeOffset LastSeenTimestampUtc,
    IReadOnlyList<string> HeardViaPathComponents,
    string? QConstruct,
    RfDiagnosticLinkState LinkState,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors);
