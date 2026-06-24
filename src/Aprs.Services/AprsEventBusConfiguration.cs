namespace Aprs.Services;

public sealed record AprsEventBusConfiguration(int MaximumRecentEvents)
{
    public static AprsEventBusConfiguration Default { get; } = new(500);
}
