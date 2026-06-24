namespace Aprs.Mapping;

public sealed record MapTileDescriptor(
    string ProviderName,
    int ZoomLevel,
    int TileX,
    int TileY,
    string? TileUrl);
