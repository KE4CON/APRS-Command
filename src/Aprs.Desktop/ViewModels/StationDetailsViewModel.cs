using System.Linq;
using Aprs.Mapping;
using Aprs.Services;

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

        // Dead reckoning — project the station's current position forward
        // from the last known fix using course and speed.
        var snapshot = new StationSnapshot(
            Callsign:              station.Callsign,
            Ssid:                  null,
            RealCallsign:          station.Callsign,
            TacticalLabel:         null,
            DisplayName:           station.DisplayName,
            LifecycleState:        station.AgeState,
            IsManuallyHidden:      false,
            Latitude:              station.Latitude,
            Longitude:             station.Longitude,
            SymbolTableIdentifier: station.SymbolTableIdentifier,
            SymbolCode:            station.SymbolCode,
            Comment:               station.Comment,
            LastHeardUtc:          station.LastHeardUtc,
            LastPacketUtc:         station.LastHeardUtc,
            LastRawPacket:         station.LastRawPacket,
            LastPacketType:        null,
            CourseDegrees:         station.CourseDegrees,
            SpeedKnots:            station.SpeedKnots,
            AltitudeFeet:          station.AltitudeFeet,
            PacketCount:           station.PacketCount,
            DuplicatePacketCount:  station.DuplicatePacketCount,
            SourcePath:            station.LastPath,
            PacketSource:          station.PacketSource,
            HasMessagingCapability: null,
            Weather:               null);

        var dr = DeadReckoningService.Project(snapshot, now);
        if (dr is not null)
        {
            var preferMiles = Configuration.StationProfile.Load().DistanceUnit
                              == Configuration.DistanceUnit.Miles;
            var distStr = preferMiles
                ? $"{dr.DistanceKm * 0.621371:F1} mi"
                : $"{dr.DistanceKm:F1} km";
            DeadReckonedSummary =
                $"Est. {distStr} on {dr.CourseDegrees}° at {dr.SpeedKnots} kt " +
                $"(based on fix {(int)dr.DataAge.TotalMinutes} min ago)";
            HasDeadReckoning = true;
        }
        else
        {
            DeadReckonedSummary = null;
            HasDeadReckoning = false;
        }

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
        DuplicatePacketCount = station.DuplicatePacketCount;
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

    /// <summary>Number of duplicate packets seen from this station (same payload, different path/time).</summary>
    public int DuplicatePacketCount { get; }

    /// <summary>True when at least one duplicate packet has been detected.</summary>
    public bool HasDuplicates => DuplicatePacketCount > 0;

    public DateTimeOffset LastHeardUtc { get; }

    public string LastHeardAge { get; }

    public string AgeState { get; }

    public string SpeedCourse { get; }

    public string Altitude { get; }

    /// <summary>
    /// Dead-reckoned position estimate, or null if projection is not possible
    /// (station is stationary, speed unknown, or data too old).
    /// </summary>
    public string? DeadReckonedSummary { get; }

    /// <summary>True when a dead-reckoned estimate is available for this station.</summary>
    public bool HasDeadReckoning { get; }

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
