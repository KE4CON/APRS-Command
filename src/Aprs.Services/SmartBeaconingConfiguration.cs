namespace Aprs.Services;

public sealed record SmartBeaconingConfiguration(
    bool Enabled,
    double LowSpeedThresholdKnots,
    double HighSpeedThresholdKnots,
    TimeSpan SlowRateInterval,
    TimeSpan FastRateInterval,
    double MinimumTurnAngleDegrees,
    double TurnSlope,
    TimeSpan TurnTimeMinimum,
    TimeSpan TurnTimeMaximum,
    TimeSpan MinimumBeaconInterval,
    TimeSpan MaximumBeaconInterval,
    bool EnabledForAprsIs,
    bool EnabledForRf)
{
    public static SmartBeaconingConfiguration Default { get; } = new(
        Enabled: false,
        LowSpeedThresholdKnots: 5,
        HighSpeedThresholdKnots: 60,
        SlowRateInterval: TimeSpan.FromMinutes(30),
        FastRateInterval: TimeSpan.FromMinutes(5),
        MinimumTurnAngleDegrees: 30,
        TurnSlope: 240,
        TurnTimeMinimum: TimeSpan.FromSeconds(30),
        TurnTimeMaximum: TimeSpan.FromMinutes(5),
        MinimumBeaconInterval: TimeSpan.FromMinutes(5),
        MaximumBeaconInterval: TimeSpan.FromMinutes(30),
        EnabledForAprsIs: false,
        EnabledForRf: false);
}
