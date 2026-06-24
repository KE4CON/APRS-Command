namespace Aprs.Services;

public sealed record DigipeaterDecisionRecord(
    string RawPacket,
    string ParsedPacketType,
    string SourceCallsign,
    string Destination,
    IReadOnlyList<string> OriginalPath,
    IReadOnlyList<string> ModifiedPath,
    string? ModifiedPacket,
    DateTimeOffset ReceivedTimestampUtc,
    string ReceivedRfPort,
    string? TransmitRfPort,
    DigipeaterDecision Decision,
    string Reason,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors,
    bool TransmitAttempted,
    BeaconNowResult? TransmitResult);
