namespace Aprs.Services;

public sealed class SmartBeaconingDecisionService : ISmartBeaconingDecisionService
{
    private readonly SmartBeaconingConfiguration configuration;
    private MobilePositionInput? previousPosition;
    private MobilePositionInput? lastBeaconPosition;
    private DateTimeOffset? lastBeaconTimeUtc;

    public SmartBeaconingDecisionService(SmartBeaconingConfiguration? configuration = null)
    {
        this.configuration = configuration ?? SmartBeaconingConfiguration.Default;
    }

    public SmartBeaconingDecision Evaluate(MobilePositionInput currentPosition)
    {
        var messages = ValidatePosition(currentPosition);
        if (!configuration.Enabled)
        {
            return Decision(
                shouldBeacon: false,
                "SmartBeaconing is disabled.",
                currentPosition,
                elapsed: null,
                turnAngle: null,
                nextRecommendedBeaconTimeUtc: null,
                ["SmartBeaconing is disabled."]);
        }

        if (messages.Count > 0)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: false,
                "Mobile position is not valid for SmartBeaconing.",
                currentPosition,
                elapsed: null,
                turnAngle: null,
                nextRecommendedBeaconTimeUtc: null,
                messages);
        }

        if (lastBeaconTimeUtc is null)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: true,
                "Initial valid mobile position.",
                currentPosition,
                elapsed: null,
                turnAngle: null,
                nextRecommendedBeaconTimeUtc: currentPosition.TimestampUtc,
                []);
        }

        var elapsed = currentPosition.TimestampUtc - lastBeaconTimeUtc.Value;
        if (elapsed < TimeSpan.Zero)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: false,
                "Mobile position timestamp is older than the last beacon.",
                currentPosition,
                elapsed,
                turnAngle: null,
                nextRecommendedBeaconTimeUtc: lastBeaconTimeUtc.Value.Add(configuration.MinimumBeaconInterval),
                ["Mobile position timestamp is older than the last beacon."]);
        }

        var effectiveInterval = CalculateSpeedBasedInterval(currentPosition.SpeedKnots.GetValueOrDefault());
        var nextByRate = lastBeaconTimeUtc.Value.Add(effectiveInterval);
        var nextByMaximum = lastBeaconTimeUtc.Value.Add(configuration.MaximumBeaconInterval);
        var nextRecommended = nextByRate <= nextByMaximum ? nextByRate : nextByMaximum;

        if (elapsed < configuration.MinimumBeaconInterval)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: false,
                "Minimum beacon interval has not elapsed.",
                currentPosition,
                elapsed,
                CalculateTurnAngle(currentPosition),
                lastBeaconTimeUtc.Value.Add(configuration.MinimumBeaconInterval),
                []);
        }

        if (elapsed >= configuration.MaximumBeaconInterval)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: true,
                "Maximum beacon interval elapsed.",
                currentPosition,
                elapsed,
                CalculateTurnAngle(currentPosition),
                nextRecommended,
                []);
        }

        var turnAngle = CalculateTurnAngle(currentPosition);
        if (turnAngle is not null
            && elapsed >= configuration.TurnTimeMinimum
            && elapsed <= configuration.TurnTimeMaximum
            && turnAngle.Value >= CalculateRequiredTurnAngle(currentPosition.SpeedKnots.GetValueOrDefault()))
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: true,
                "Heading change exceeds SmartBeaconing turn threshold.",
                currentPosition,
                elapsed,
                turnAngle,
                nextRecommended,
                []);
        }

        if (elapsed >= effectiveInterval)
        {
            previousPosition = currentPosition;
            return Decision(
                shouldBeacon: true,
                "Speed-based beacon interval elapsed.",
                currentPosition,
                elapsed,
                turnAngle,
                nextRecommended,
                []);
        }

        previousPosition = currentPosition;
        return Decision(
            shouldBeacon: false,
            "No SmartBeaconing trigger is due.",
            currentPosition,
            elapsed,
            turnAngle,
            nextRecommended,
            []);
    }

    public void RecordBeacon(MobilePositionInput beaconedPosition)
    {
        lastBeaconPosition = beaconedPosition;
        lastBeaconTimeUtc = beaconedPosition.TimestampUtc;
        previousPosition = beaconedPosition;
    }

    public void Reset()
    {
        previousPosition = null;
        lastBeaconPosition = null;
        lastBeaconTimeUtc = null;
    }

    private TimeSpan CalculateSpeedBasedInterval(double speedKnots)
    {
        if (speedKnots <= configuration.LowSpeedThresholdKnots)
        {
            return configuration.SlowRateInterval;
        }

        if (speedKnots >= configuration.HighSpeedThresholdKnots)
        {
            return configuration.FastRateInterval;
        }

        var speedRange = configuration.HighSpeedThresholdKnots - configuration.LowSpeedThresholdKnots;
        if (speedRange <= 0)
        {
            return configuration.SlowRateInterval;
        }

        var ratio = (speedKnots - configuration.LowSpeedThresholdKnots) / speedRange;
        var slowTicks = configuration.SlowRateInterval.Ticks;
        var fastTicks = configuration.FastRateInterval.Ticks;
        var interpolatedTicks = slowTicks - ((slowTicks - fastTicks) * ratio);
        return TimeSpan.FromTicks(Math.Max(configuration.MinimumBeaconInterval.Ticks, (long)interpolatedTicks));
    }

    private double CalculateRequiredTurnAngle(double speedKnots)
    {
        var speed = Math.Max(1, speedKnots);
        return Math.Min(120, configuration.MinimumTurnAngleDegrees + (configuration.TurnSlope / speed));
    }

    private double? CalculateTurnAngle(MobilePositionInput currentPosition)
    {
        var referenceCourse = lastBeaconPosition?.CourseDegrees ?? previousPosition?.CourseDegrees;
        if (referenceCourse is null || currentPosition.CourseDegrees is null)
        {
            return null;
        }

        var difference = Math.Abs(NormalizeCourse(currentPosition.CourseDegrees.Value) - NormalizeCourse(referenceCourse.Value));
        return difference > 180 ? 360 - difference : difference;
    }

    private static double NormalizeCourse(double courseDegrees)
    {
        var normalized = courseDegrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static List<string> ValidatePosition(MobilePositionInput currentPosition)
    {
        var messages = new List<string>();
        if (!currentPosition.FixValid)
        {
            messages.Add("GPS fix is not valid.");
        }

        if (currentPosition.Latitude is < -90 or > 90)
        {
            messages.Add("Latitude must be between -90 and 90 degrees.");
        }

        if (currentPosition.Longitude is < -180 or > 180)
        {
            messages.Add("Longitude must be between -180 and 180 degrees.");
        }

        if (currentPosition.SpeedKnots is < 0)
        {
            messages.Add("Speed cannot be negative.");
        }

        if (currentPosition.CourseDegrees is < 0 or >= 360)
        {
            messages.Add("Course must be between 0 and 359 degrees.");
        }

        return messages;
    }

    private static SmartBeaconingDecision Decision(
        bool shouldBeacon,
        string reason,
        MobilePositionInput currentPosition,
        TimeSpan? elapsed,
        double? turnAngle,
        DateTimeOffset? nextRecommendedBeaconTimeUtc,
        IReadOnlyList<string> messages)
    {
        return new SmartBeaconingDecision(
            shouldBeacon,
            reason,
            nextRecommendedBeaconTimeUtc,
            currentPosition.SpeedKnots,
            currentPosition.CourseDegrees,
            elapsed,
            turnAngle,
            messages);
    }
}
