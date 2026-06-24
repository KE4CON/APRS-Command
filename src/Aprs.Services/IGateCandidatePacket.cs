namespace Aprs.Services;

public sealed record IGateCandidatePacket(
    string RawPacket,
    string ParsedPacketType,
    string SourceCallsign,
    string Destination,
    IReadOnlyList<string> Path,
    string? QConstruct,
    DateTimeOffset ReceivedTimestampUtc,
    string ReceivedSourcePort,
    AprsPacketSource PacketSource,
    bool IsRfSource,
    bool WasAlsoSeenOnAprsIs,
    IGateDuplicateState DuplicateState,
    IGateCandidateState CandidateState,
    string Reason,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors);
