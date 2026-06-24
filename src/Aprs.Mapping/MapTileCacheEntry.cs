namespace Aprs.Mapping;

public sealed record MapTileCacheEntry(
    string ProviderName,
    int ZoomLevel,
    int TileX,
    int TileY,
    string? TileUrl,
    string LocalCachePath,
    DateTimeOffset? CachedAtUtc,
    long TileSizeBytes,
    DateTimeOffset? ExpiresAtUtc,
    bool ExistsInCache);
