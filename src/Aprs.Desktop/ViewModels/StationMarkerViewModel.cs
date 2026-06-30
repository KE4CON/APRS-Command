using Aprs.Mapping;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class StationMarkerViewModel
{
    private const double LongitudeMin = -180;
    private const double LongitudeMax = 180;
    private const double LatitudeMin = -90;
    private const double LatitudeMax = 90;

    public StationMarkerViewModel(StationMarker marker)
    {
        Callsign = marker.Callsign;
        DisplayName = marker.DisplayName;
        Latitude = marker.Latitude;
        Longitude = marker.Longitude;
        SymbolTableIdentifier = marker.SymbolTableIdentifier;
        SymbolCode = marker.SymbolCode;
        Overlay = marker.Overlay;
        SymbolDescription = marker.SymbolDescription;
        SymbolCategory = marker.SymbolCategory;
        MarkerIconKey = marker.MarkerIconKey;
        FallbackMarkerText = marker.FallbackMarkerText;
        LastHeardUtc = marker.LastHeardUtc;
        AgeState = marker.AgeState;
        PacketSource = marker.PacketSource;
        CourseDegrees = marker.CourseDegrees;
        SpeedKnots = marker.SpeedKnots;
        AltitudeFeet = marker.AltitudeFeet;
        LastPath = marker.LastPath;
        DuplicatePacketCount = marker.DuplicatePacketCount;
        Comment = marker.Comment;
        LastRawPacket = marker.LastRawPacket;
        PacketCount = marker.PacketCount;
        IsLoRa = marker.IsLoRa;
    }

    public string Callsign { get; }

    public string DisplayName { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public char? SymbolTableIdentifier { get; }

    public char? SymbolCode { get; }

    public char? Overlay { get; }

    public string SymbolDescription { get; }

    public AprsSymbolCategory SymbolCategory { get; }

    public string MarkerIconKey { get; }

    public string FallbackMarkerText { get; }

    public DateTimeOffset LastHeardUtc { get; }

    public StationLifecycleState AgeState { get; }

    public AprsPacketSource PacketSource { get; }

    public int? CourseDegrees { get; }

    public int? SpeedKnots { get; }

    public int? AltitudeFeet { get; }

    public IReadOnlyList<string> LastPath { get; }
    public int DuplicatePacketCount { get; }

    public string? Comment { get; }

    public string? LastRawPacket { get; }

    public int PacketCount { get; }
    public bool IsLoRa { get; }

    public string SymbolLabel => Overlay is null
        ? FallbackMarkerText
        : $"{Overlay}{FallbackMarkerText}";

    public string SourceLabel => PacketSource.ToString();

    public string MovementLabel => CourseDegrees is null && SpeedKnots is null
        ? "Stationary"
        : $"{CourseDegrees?.ToString() ?? "---"} deg / {SpeedKnots?.ToString() ?? "--"} kt";

    public double MapLeftPercent => Normalize(Longitude, LongitudeMin, LongitudeMax) * 100;

    public double MapTopPercent => (1 - Normalize(Latitude, LatitudeMin, LatitudeMax)) * 100;

    private static double Normalize(double value, double minimum, double maximum)
    {
        var normalized = (value - minimum) / (maximum - minimum);
        return Math.Clamp(normalized, 0, 1);
    }
}
