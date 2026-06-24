namespace Aprs.Services;

public sealed record AprsBulletinConfiguration(
    TimeSpan? BulletinLifetime = null,
    TimeSpan? AnnouncementLifetime = null)
{
    public static AprsBulletinConfiguration Default { get; } = new(
        BulletinLifetime: TimeSpan.FromHours(12),
        AnnouncementLifetime: TimeSpan.FromHours(12));
}
