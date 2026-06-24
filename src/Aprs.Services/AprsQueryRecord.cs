namespace Aprs.Services;

public sealed record AprsQueryRecord(
    string QueryType,
    string SenderCallsign,
    string QueryBody,
    string RawPacket,
    DateTimeOffset ReceivedAtUtc,
    AprsPacketSource Source,
    IReadOnlyList<string> ValidationErrors);
