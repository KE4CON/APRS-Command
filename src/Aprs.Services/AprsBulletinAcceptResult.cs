namespace Aprs.Services;

public sealed record AprsBulletinAcceptResult(
    AprsBulletinRecord? Bulletin,
    AprsAnnouncementRecord? Announcement,
    AprsQueryRecord? Query)
{
    public bool WasHandled => Bulletin is not null || Announcement is not null || Query is not null;

    public static AprsBulletinAcceptResult NotHandled { get; } = new(null, null, null);
}
