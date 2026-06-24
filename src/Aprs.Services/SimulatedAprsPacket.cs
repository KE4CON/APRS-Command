namespace Aprs.Services;

public sealed record SimulatedAprsPacket(
    Guid PacketId,
    string RawPacket,
    AprsPacketSource PacketSource,
    DateTimeOffset GeneratedAtUtc,
    string SimulationSourceName,
    string PacketKind,
    string? EntityName);
