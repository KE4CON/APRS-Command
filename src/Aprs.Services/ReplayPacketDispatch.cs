namespace Aprs.Services;

public sealed record ReplayPacketDispatch(
    ReplayLogEntry Entry,
    string RawPacketText,
    DateTimeOffset ReplayTimestampUtc,
    AprsPacketSource PacketSource);
