namespace Aprs.Transport;

public sealed record KissFrame(
    IReadOnlyList<byte> RawFrameBytes,
    int PortNumber,
    KissCommandType CommandType,
    IReadOnlyList<byte> Payload,
    string? DecodedAprsPacketText,
    DateTimeOffset TimestampUtc,
    string SourceName,
    IReadOnlyList<string> ValidationErrors)
{
    public bool IsValid => ValidationErrors.Count == 0;
}
