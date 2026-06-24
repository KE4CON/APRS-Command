using Aprs.Transport;

namespace Aprs.Services;

public sealed record IGateGatingDecisionRecord(
    string RawPacket,
    string ParsedPacketType,
    string SourceCallsign,
    string Destination,
    IReadOnlyList<string> Path,
    DateTimeOffset ReceivedTimestampUtc,
    string ReceivedRfPort,
    IGateCandidateState CandidateState,
    IGateDecision Decision,
    string Reason,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors,
    bool TransmitAttempted,
    AprsIsTransmitResult? TransmitResult);
