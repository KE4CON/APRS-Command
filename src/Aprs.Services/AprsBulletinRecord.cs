namespace Aprs.Services;

public sealed record AprsBulletinRecord(
    string BulletinId,
    string SenderCallsign,
    string Addressee,
    string BulletinText,
    string RawPacket,
    DateTimeOffset ReceivedAtUtc,
    AprsPacketSource Source,
    DateTimeOffset LastUpdatedAtUtc,
    bool IsActive,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<string> ValidationErrors);
