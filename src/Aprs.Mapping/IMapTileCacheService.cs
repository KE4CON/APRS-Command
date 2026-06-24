namespace Aprs.Mapping;

public interface IMapTileCacheService
{
    /// <summary>
    /// Creates a stable cache key for one map tile.
    /// </summary>
    string GenerateCacheKey(MapTileDescriptor tile);

    /// <summary>
    /// Calculates the local cache file path for one map tile.
    /// </summary>
    string GetLocalCachePath(MapTileDescriptor tile);

    /// <summary>
    /// Gets cache metadata for one map tile.
    /// </summary>
    MapTileCacheEntry GetTileInfo(MapTileDescriptor tile, DateTimeOffset now);

    /// <summary>
    /// Checks whether one map tile exists in the local cache.
    /// </summary>
    bool TileExists(MapTileDescriptor tile);

    /// <summary>
    /// Saves tile bytes to the local cache.
    /// </summary>
    MapTileCacheEntry SaveTile(MapTileDescriptor tile, byte[] bytes, DateTimeOffset cachedAtUtc);

    /// <summary>
    /// Reads tile bytes from the local cache, or returns null when missing.
    /// </summary>
    byte[]? ReadTile(MapTileDescriptor tile);

    /// <summary>
    /// Deletes one cached tile.
    /// </summary>
    bool DeleteTile(MapTileDescriptor tile);

    /// <summary>
    /// Clears all cached tiles beneath the configured cache root.
    /// </summary>
    void Clear();

    /// <summary>
    /// Calculates the approximate local cache size in bytes.
    /// </summary>
    long GetCacheSizeBytes();

    /// <summary>
    /// Enforces the configured maximum cache size by deleting oldest cached files first.
    /// </summary>
    void EnforceMaximumCacheSize();
}
