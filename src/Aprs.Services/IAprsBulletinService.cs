using Aprs.Core;

namespace Aprs.Services;

public interface IAprsBulletinService
{
    /// <summary>
    /// Accepts a parsed APRS packet and stores bulletins, announcements, or queries when applicable.
    /// </summary>
    AprsBulletinAcceptResult AcceptPacket(AprsPacket packet, AprsPacketSource source = AprsPacketSource.Unknown);

    IReadOnlyList<AprsBulletinRecord> GetAllBulletins();

    IReadOnlyList<AprsBulletinRecord> GetActiveBulletins(DateTimeOffset now);

    IReadOnlyList<AprsAnnouncementRecord> GetAllAnnouncements();

    IReadOnlyList<AprsAnnouncementRecord> GetActiveAnnouncements(DateTimeOffset now);

    IReadOnlyList<AprsQueryRecord> GetQueries();

    int ClearExpired(DateTimeOffset now);

    void Clear();
}
