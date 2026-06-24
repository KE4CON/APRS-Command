namespace Aprs.Services;

public sealed record MobilePositionInput(
    double Latitude,
    double Longitude,
    DateTimeOffset TimestampUtc,
    double? SpeedKnots,
    double? CourseDegrees,
    int? AltitudeFeet,
    bool FixValid,
    MobilePositionSource Source);
