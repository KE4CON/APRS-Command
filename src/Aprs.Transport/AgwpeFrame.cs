namespace Aprs.Transport;

public sealed record AgwpeFrame(
    IReadOnlyList<byte> RawFrameBytes,
    char CommandType,
    int RadioPort,
    string? SourceCallsign,
    string? DestinationCallsign,
    IReadOnlyList<string> Path,
    IReadOnlyList<byte> Payload,
    string? DecodedAprsPacketText,
    DateTimeOffset TimestampUtc,
    string PacketSource,
    IReadOnlyList<string> ValidationErrors)
{
    public bool IsValid => ValidationErrors.Count == 0;
}
