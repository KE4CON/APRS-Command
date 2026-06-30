using Aprs.Services;

namespace Aprs.Mapping;

public sealed record StationMarker(
    string Callsign,
    string DisplayName,
    double Latitude,
    double Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    char? Overlay,
    string SymbolDescription,
    AprsSymbolCategory SymbolCategory,
    string MarkerIconKey,
    string FallbackMarkerText,
    DateTimeOffset LastHeardUtc,
    StationLifecycleState AgeState,
    AprsPacketSource PacketSource,
    int? CourseDegrees,
    int? SpeedKnots,
    int? AltitudeFeet,
    IReadOnlyList<string> LastPath,
    string? Comment,
    string? LastRawPacket,
    int PacketCount,
    int DuplicatePacketCount,
    bool IsLoRa = false)
{
    public static StationMarker Create(
        string callsign,
        string displayName,
        double latitude,
        double longitude,
        char? symbolTableIdentifier,
        char? symbolCode,
        DateTimeOffset lastHeardUtc,
        StationLifecycleState ageState,
        AprsPacketSource packetSource,
        int? CourseDegrees,
        int? SpeedKnots,
        int? altitudeFeet = null,
        IReadOnlyList<string>? lastPath = null,
        string? comment = null,
        string? lastRawPacket = null,
        int packetCount = 0,
        int duplicatePacketCount = 0,
        bool isLoRa = false)
    {
        var symbol = AprsSymbolLookupService.Default.Resolve(symbolTableIdentifier, symbolCode);

        return new StationMarker(
            callsign,
            displayName,
            latitude,
            longitude,
            symbolTableIdentifier,
            symbolCode,
            symbol.Overlay,
            symbol.Description,
            symbol.Category,
            symbol.MarkerIconKey,
            symbol.FallbackDisplayText,
            lastHeardUtc,
            ageState,
            packetSource,
            CourseDegrees,
            SpeedKnots,
            altitudeFeet,
            lastPath ?? [],
            comment,
            lastRawPacket,
            packetCount,
            duplicatePacketCount,
            isLoRa);
    }

    public static bool TryCreate(StationSnapshot station, out StationMarker? marker)
    {
        if (station.Latitude is null || station.Longitude is null)
        {
            marker = null;
            return false;
        }

        marker = Create(
            station.RealCallsign,
            station.DisplayName,
            station.Latitude.Value,
            station.Longitude.Value,
            station.SymbolTableIdentifier,
            station.SymbolCode,
            station.LastHeardUtc,
            station.LifecycleState,
            station.PacketSource,
            station.CourseDegrees,
            station.SpeedKnots,
            station.AltitudeFeet,
            station.SourcePath,
            station.Comment,
            station.LastRawPacket,
            station.PacketCount,
            station.DuplicatePacketCount,
            station.IsLoRa);

        return true;
    }
}
