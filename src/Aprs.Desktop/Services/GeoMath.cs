namespace Aprs.Desktop.Services;

/// <summary>
/// Geographic math utilities — Haversine distance, initial bearing, and
/// cardinal direction formatting. Used by the station detail panel and
/// the measure-distance tool.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>Returns the great-circle distance in kilometres between two lat/lon points.</summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Returns the distance in miles.</summary>
    public static double HaversineMi(double lat1, double lon1, double lat2, double lon2)
        => HaversineKm(lat1, lon1, lat2, lon2) * 0.621371;

    /// <summary>Returns the initial bearing in degrees (0–360) from point 1 to point 2.</summary>
    public static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(dLon) * Math.Cos(lat2 * Math.PI / 180);
        var x = Math.Cos(lat1 * Math.PI / 180) * Math.Sin(lat2 * Math.PI / 180) -
                Math.Sin(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>Converts a bearing in degrees to a 16-point cardinal direction string.</summary>
    public static string CardinalBearing(double deg) => (int)(deg / 22.5 + 0.5) switch
    {
        0 or 16 => "N",   1 => "NNE", 2 => "NE",  3 => "ENE",
        4       => "E",   5 => "ESE", 6 => "SE",  7 => "SSE",
        8       => "S",   9 => "SSW", 10 => "SW", 11 => "WSW",
        12      => "W",  13 => "WNW", 14 => "NW", 15 => "NNW",
        _       => "N"
    };

    /// <summary>
    /// Formats distance and bearing from the operator's station to a remote station
    /// as a human-readable string, e.g. "12.4 mi / 247° SW".
    /// Returns null if either position is unavailable.
    /// </summary>
    public static (string Distance, string Bearing)? FromMyStation(
        double myLat, double myLon,
        double remoteLat, double remoteLon)
    {
        if (myLat == 0 && myLon == 0) return null;
        if (remoteLat == 0 && remoteLon == 0) return null;

        var distMi  = HaversineMi(myLat, myLon, remoteLat, remoteLon);
        var distKm  = HaversineKm(myLat, myLon, remoteLat, remoteLon);
        var bearing = BearingDeg(myLat, myLon, remoteLat, remoteLon);
        var cardinal = CardinalBearing(bearing);

        return (
            distMi < 1
                ? $"{distMi * 5280:F0} ft  ({distKm * 1000:F0} m)"
                : $"{distMi:F1} mi  ({distKm:F1} km)",
            $"{bearing:F0}°  {cardinal}"
        );
    }
}
