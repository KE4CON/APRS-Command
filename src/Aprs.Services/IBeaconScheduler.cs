namespace Aprs.Services;

public interface IBeaconScheduler
{
    /// <summary>
    /// Gets the current beacon scheduler state.
    /// </summary>
    BeaconSchedulerState GetState();

    /// <summary>
    /// Enables scheduling and calculates the next beacon times.
    /// </summary>
    BeaconSchedulerState Start();

    /// <summary>
    /// Disables scheduling and clears pending beacon times.
    /// </summary>
    BeaconSchedulerState Stop();

    /// <summary>
    /// Generates a beacon immediately and transmits it only when every safety check passes.
    /// </summary>
    Task<BeaconNowResult> BeaconNowAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates due scheduled beacons without requiring real-time delays.
    /// </summary>
    Task<BeaconNowResult?> TickAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates whether SmartBeaconing recommends a mobile beacon without transmitting.
    /// </summary>
    SmartBeaconingDecision EvaluateSmartBeaconing(MobilePositionInput currentPosition);
}
