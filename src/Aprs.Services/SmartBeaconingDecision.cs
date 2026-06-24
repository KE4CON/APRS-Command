namespace Aprs.Services;

public sealed record SmartBeaconingDecision(
    bool ShouldBeacon,
    string Reason,
    DateTimeOffset? NextRecommendedBeaconTimeUtc,
    double? CurrentSpeedKnots,
    double? CurrentCourseDegrees,
    TimeSpan? ElapsedSinceLastBeacon,
    double? TurnAngleDegrees,
    IReadOnlyList<string> ValidationMessages);
