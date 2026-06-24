namespace Aprs.Mapping;

public sealed record MapMarker(
    string Id,
    string Label,
    double Latitude,
    double Longitude,
    string SymbolCode,
    DateTimeOffset LastUpdatedUtc);
