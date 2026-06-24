namespace Aprs.Services;

public sealed record WeatherDisplayConfiguration(
    TimeSpan CurrentThreshold,
    TimeSpan ExpiredThreshold,
    bool ExcludeStaleFromCurrentList)
{
    public static WeatherDisplayConfiguration Default { get; } = new(
        CurrentThreshold: TimeSpan.FromMinutes(15),
        ExpiredThreshold: TimeSpan.FromHours(2),
        ExcludeStaleFromCurrentList: true);
}
