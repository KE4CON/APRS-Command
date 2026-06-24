namespace Aprs.Services;

public interface IBeaconSchedulerClock
{
    /// <summary>
    /// Gets the current UTC time for beacon scheduling decisions.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
