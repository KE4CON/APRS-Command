using System.Linq;
using Aprs.Mapping;

namespace Aprs.Desktop.ViewModels;

public sealed class StationDetailsViewModel
{
    public StationDetailsViewModel(StationMarkerViewModel station, DateTimeOffset now)
    {
        Callsign = station.Callsign;
        DisplayName = station.DisplayName;
        TacticalLabel = station.DisplayName == station.Callsign ? "None" : station.DisplayName;
        SymbolDescription = station.SymbolDescription;
        Latitude = station.Latitude;
        Longitude = station.Longitude;
        MaidenheadGridSquare = MaidenheadGridLocator.FromCoordinates(station.Latitude, station.Longitude);
        LastHeardUtc = station.LastHeardUtc;
        AgeState = station.AgeState.ToString();
        PacketSource = station.SourceLabel;
        LastPath = station.LastPath.Count == 0 ? "None" : string.Join(",", station.LastPath);
        Comment = FormatOptional(station.Comment);
        LastRawPacket = FormatOptional(station.LastRawPacket);
        PacketCount = station.PacketCount.ToString();
        Coordinates = $"{FormatLatitude(station.Latitude)}, {FormatLongitude(station.Longitude)}";
        LastHeardAge = FormatAge(now - station.LastHeardUtc);
        SpeedCourse = station.SpeedKnots is null && station.CourseDegrees is null
            ? "Unknown"
            : $"{station.SpeedKnots?.ToString() ?? "--"} kt / {station.CourseDegrees?.ToString() ?? "---"} deg";
        Altitude = station.AltitudeFeet is null ? "Unknown" : $"{station.AltitudeFeet} ft";
        DistanceFromMyStation = "Unknown";
        BearingFromMyStation  = "Unknown";

        // Parse heard-by digipeaters from path — entries marked with * were actually relayed.
        var heardBy = station.LastPath
            .Where(p => p.EndsWith('*'))
            .Select(p => p.TrimEnd('*'))
            .Where(p => !p.StartsWith("WIDE",  StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("TRACE", StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("RELAY", StringComparison.OrdinalIgnoreCase))
            .ToList();
        HeardBy = heardBy.Count > 0
            ? string.Join(", ", heardBy)
            : station.LastPath.Count == 0 ? "Direct / APRS-IS" : "Direct (no digipeater)";
    }

    public StationDetailsViewModel(StationMarkerViewModel station, DateTimeOffset now,
        double myLat, double myLon)
        : this(station, now)
    {
        var result = Services.GeoMath.FromMyStation(myLat, myLon, station.Latitude, station.Longitude);
        if (result.HasValue)
        {
            DistanceFromMyStation = result.Value.Distance;
            BearingFromMyStation  = result.Value.Bearing;
        }
    }

    public string Callsign { get; }

    public string DisplayName { get; }

    public string TacticalLabel { get; }

    public string SymbolDescription { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public string Coordinates { get; }

    public string MaidenheadGridSquare { get; }

    public string DistanceFromMyStation { get; private set; }

    public string BearingFromMyStation { get; private set; }

    /// <summary>Digipeaters that heard and relayed the last packet (from the APRS path).</summary>
    public string HeardBy { get; }

    public DateTimeOffset LastHeardUtc { get; }

    public string LastHeardAge { get; }

    public string AgeState { get; }

    public string SpeedCourse { get; }

    public string Altitude { get; }

    public string PacketSource { get; }

    public string LastPath { get; }

    public string Comment { get; }

    public string LastRawPacket { get; }

    public string PacketCount { get; }

    private static string FormatLatitude(double latitude)
    {
        var hemisphere = latitude >= 0 ? "N" : "S";
        return $"{Math.Abs(latitude):0.00000} {hemisphere}";
    }

    private static string FormatLongitude(double longitude)
    {
        var hemisphere = longitude >= 0 ? "E" : "W";
        return $"{Math.Abs(longitude):0.00000} {hemisphere}";
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} min ago";
        }

        if (age.TotalDays < 1)
        {
            return $"{(int)age.TotalHours} hr ago";
        }

        return $"{(int)age.TotalDays} d ago";
    }

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }
}
