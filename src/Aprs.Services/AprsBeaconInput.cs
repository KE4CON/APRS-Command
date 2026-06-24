namespace Aprs.Services;

public sealed record AprsBeaconInput(
    string SourceStationIdentifier,
    string Destination,
    IReadOnlyList<string> Path,
    double? Latitude,
    double? Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    string? Comment,
    int? AltitudeFeet,
    int? CourseDegrees,
    int? SpeedKnots,
    string? PhgData,
    bool UseTimestamp,
    bool UseCompressedPosition,
    bool RfPathRequired);
