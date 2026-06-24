namespace Aprs.Mapping;

public sealed record OfflineMapDownloadEstimate(
    OfflineMapDownloadArea Area,
    IReadOnlyList<MapTileRange> TileRanges,
    IReadOnlyList<MapTileDescriptor> Tiles,
    long TotalTiles,
    long EstimatedSizeBytes,
    bool IsLargeDownload,
    IReadOnlyList<string> Warnings);
