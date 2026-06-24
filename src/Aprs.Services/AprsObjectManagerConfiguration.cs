namespace Aprs.Services;

public sealed record AprsObjectManagerConfiguration(
    TimeSpan StaleThreshold,
    TimeSpan ExpiredThreshold)
{
    public static AprsObjectManagerConfiguration Default { get; } = new(
        StaleThreshold: TimeSpan.FromHours(2),
        ExpiredThreshold: TimeSpan.FromHours(8));
}
