namespace Aprs.Services;

/// <summary>
/// Projects a station's current position forward in time based on its last known
/// course and speed — a technique known as dead reckoning. Returns null if the
/// station has no course or speed data, or if the projection would be unreliable
/// (station heard too long ago, speed too low, etc.).
/// </summary>
public static class DeadReckoningService
{
    private const double EarthRadiusKm = 6371.0;
    private const double KnotsToKmPerHour = 1.852;

    /// <summary>
    /// Returns a dead-reckoned position for the given station snapshot.
    /// Returns null if projection is not possible or would be unreliable.
    /// </summary>
    /// <param name="snapshot">The most recent station snapshot.</param>
    /// <param name="now">The current time.</param>
    /// <param name="maxAgeMinutes">Maximum age of the snapshot before projection is considered unreliable.</param>
    public static DeadReckonedPosition? Project(
        StationSnapshot snapshot,
        DateTimeOffset now,
        double maxAgeMinutes = 30.0)
    {
        if (snapshot.Latitude  is null || snapshot.Longitude is null) return null;
        if (snapshot.CourseDegrees is null || snapshot.SpeedKnots is null) return null;
        if (snapshot.SpeedKnots.Value < 1) return null; // stationary — no projection

        var age = now - snapshot.LastHeardUtc;
        if (age.TotalMinutes > maxAgeMinutes) return null;

        var speedKmh     = snapshot.SpeedKnots.Value * KnotsToKmPerHour;
        var elapsedHours = age.TotalHours;
        var distanceKm   = speedKmh * elapsedHours;

        var (projLat, projLon) = ProjectPosition(
            snapshot.Latitude.Value,
            snapshot.Longitude.Value,
            snapshot.CourseDegrees.Value,
            distanceKm);

        return new DeadReckonedPosition(
            Callsign:       snapshot.DisplayName,
            Latitude:       projLat,
            Longitude:      projLon,
            BasedOnLat:     snapshot.Latitude.Value,
            BasedOnLon:     snapshot.Longitude.Value,
            CourseDegrees:  snapshot.CourseDegrees.Value,
            SpeedKnots:     snapshot.SpeedKnots.Value,
            DistanceKm:     distanceKm,
            DataAge:        age,
            ProjectedAt:    now);
    }

    private static (double Lat, double Lon) ProjectPosition(
        double latDeg, double lonDeg, double courseDeg, double distanceKm)
    {
        var lat  = ToRad(latDeg);
        var lon  = ToRad(lonDeg);
        var brng = ToRad(courseDeg);
        var d    = distanceKm / EarthRadiusKm;

        var lat2 = Math.Asin(
            Math.Sin(lat) * Math.Cos(d) +
            Math.Cos(lat) * Math.Sin(d) * Math.Cos(brng));

        var lon2 = lon + Math.Atan2(
            Math.Sin(brng) * Math.Sin(d) * Math.Cos(lat),
            Math.Cos(d) - Math.Sin(lat) * Math.Sin(lat2));

        return (ToDeg(lat2), ToDeg(lon2));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}

/// <summary>Dead-reckoned position with supporting metadata for display.</summary>
public sealed record DeadReckonedPosition(
    string    Callsign,
    double    Latitude,
    double    Longitude,
    double    BasedOnLat,
    double    BasedOnLon,
    int       CourseDegrees,
    int       SpeedKnots,
    double    DistanceKm,
    TimeSpan  DataAge,
    DateTimeOffset ProjectedAt)
{
    public string Summary =>
        $"{Callsign}  projected {DistanceKm:F1} km on {CourseDegrees}° at {SpeedKnots} kt  ({(int)DataAge.TotalMinutes} min ago)";
}
