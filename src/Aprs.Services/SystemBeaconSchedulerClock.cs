namespace Aprs.Services;

public sealed class SystemBeaconSchedulerClock : IBeaconSchedulerClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
