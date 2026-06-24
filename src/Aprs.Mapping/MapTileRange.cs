namespace Aprs.Mapping;

public sealed record MapTileRange(
    int ZoomLevel,
    int MinimumTileX,
    int MaximumTileX,
    int MinimumTileY,
    int MaximumTileY)
{
    public long TileCount => ((long)MaximumTileX - MinimumTileX + 1) * (MaximumTileY - MinimumTileY + 1);
}
