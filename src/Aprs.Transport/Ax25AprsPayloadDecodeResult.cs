namespace Aprs.Transport;

public sealed record Ax25AprsPayloadDecodeResult(
    string? AprsPacketText,
    IReadOnlyList<string> ValidationErrors);
