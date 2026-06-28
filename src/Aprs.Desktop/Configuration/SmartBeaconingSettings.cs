using Aprs.Services;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted smart beaconing settings. SmartBeaconing adjusts the beacon interval
/// dynamically based on speed and course changes — faster beaconing when moving
/// quickly or turning, slower when stationary. Requires GPS.
/// </summary>
public sealed record SmartBeaconingSettings(
    bool Enabled,
    double LowSpeedThresholdKnots,
    double HighSpeedThresholdKnots,
    int SlowRateMinutes,
    int FastRateMinutes,
    double MinimumTurnAngleDegrees,
    bool EnabledForAprsIs,
    bool EnabledForRf)
{
    public static SmartBeaconingSettings Default { get; } = new(
        Enabled:                  false,
        LowSpeedThresholdKnots:   5,
        HighSpeedThresholdKnots:  60,
        SlowRateMinutes:          30,
        FastRateMinutes:          5,
        MinimumTurnAngleDegrees:  30,
        EnabledForAprsIs:         true,
        EnabledForRf:             false);

    /// <summary>Converts to the service-layer configuration record.</summary>
    public SmartBeaconingConfiguration ToServiceConfig() => new(
        Enabled:                 Enabled,
        LowSpeedThresholdKnots:  LowSpeedThresholdKnots,
        HighSpeedThresholdKnots: HighSpeedThresholdKnots,
        SlowRateInterval:        TimeSpan.FromMinutes(SlowRateMinutes),
        FastRateInterval:        TimeSpan.FromMinutes(FastRateMinutes),
        MinimumTurnAngleDegrees: MinimumTurnAngleDegrees,
        TurnSlope:               240,
        TurnTimeMinimum:         TimeSpan.FromSeconds(30),
        TurnTimeMaximum:         TimeSpan.FromMinutes(FastRateMinutes),
        MinimumBeaconInterval:   TimeSpan.FromMinutes(FastRateMinutes),
        MaximumBeaconInterval:   TimeSpan.FromMinutes(SlowRateMinutes),
        EnabledForAprsIs:        EnabledForAprsIs,
        EnabledForRf:            EnabledForRf);
}
