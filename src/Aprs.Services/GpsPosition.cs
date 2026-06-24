namespace Aprs.Services;

public sealed record GpsPosition(
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    double? SpeedKnots,
    double? CourseDegrees,
    DateTimeOffset? TimestampUtc,
    bool FixValid,
    int? FixQuality,
    int? SatelliteCount,
    double? Hdop,
    string SourceName,
    string RawNmeaSentence,
    DateTimeOffset LastUpdateUtc)
{
    public int? UsedSatelliteCount { get; init; }

    public MobilePositionInput ToMobilePositionInput()
    {
        return new MobilePositionInput(
            Latitude.GetValueOrDefault(),
            Longitude.GetValueOrDefault(),
            TimestampUtc ?? LastUpdateUtc,
            SpeedKnots,
            CourseDegrees,
            AltitudeMeters is null ? null : (int)Math.Round(AltitudeMeters.Value * 3.28084),
            FixValid && Latitude is not null && Longitude is not null,
            MobilePositionSource.Gps);
    }
}
