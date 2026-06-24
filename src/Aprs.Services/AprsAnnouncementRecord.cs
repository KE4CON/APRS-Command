namespace Aprs.Services;

public sealed record AprsAnnouncementRecord(
    string AnnouncementId,
    string SenderCallsign,
    string Addressee,
    string AnnouncementText,
    string RawPacket,
    DateTimeOffset ReceivedAtUtc,
    AprsPacketSource Source,
    DateTimeOffset LastUpdatedAtUtc,
    bool IsActive,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyList<string> ValidationErrors);
