using System.Text;

namespace Aprs.Mapping;

public sealed class MapTileCacheService : IMapTileCacheService
{
    private const int MaximumZoomLevel = 30;
    private readonly MapTileCacheConfiguration configuration;

    public MapTileCacheService(MapTileCacheConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string GenerateCacheKey(MapTileDescriptor tile)
    {
        ValidateTile(tile);

        return $"{SanitizeProviderName(tile.ProviderName)}/{tile.ZoomLevel}/{tile.TileX}/{tile.TileY}.tile";
    }

    public string GetLocalCachePath(MapTileDescriptor tile)
    {
        var cacheKey = GenerateCacheKey(tile);
        var root = Path.GetFullPath(configuration.CacheRootFolder);
        var path = Path.GetFullPath(Path.Combine(root, cacheKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !path.Equals(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tile cache path escaped the configured cache root.");
        }

        return path;
    }

    public MapTileCacheEntry GetTileInfo(MapTileDescriptor tile, DateTimeOffset now)
    {
        var path = GetLocalCachePath(tile);
        var exists = File.Exists(path);
        var cachedAt = exists ? File.GetLastWriteTimeUtc(path) : (DateTime?)null;
        var cachedAtUtc = cachedAt is null
            ? (DateTimeOffset?)null
            : new DateTimeOffset(DateTime.SpecifyKind(cachedAt.Value, DateTimeKind.Utc));
        DateTimeOffset? expiresAt = cachedAtUtc is not null && configuration.TileExpirationAge is not null
            ? cachedAtUtc.Value.Add(configuration.TileExpirationAge.Value)
            : null;

        return new MapTileCacheEntry(
            tile.ProviderName,
            tile.ZoomLevel,
            tile.TileX,
            tile.TileY,
            tile.TileUrl,
            path,
            cachedAtUtc,
            exists ? new FileInfo(path).Length : 0,
            expiresAt,
            exists);
    }

    public bool TileExists(MapTileDescriptor tile)
    {
        return File.Exists(GetLocalCachePath(tile));
    }

    public MapTileCacheEntry SaveTile(MapTileDescriptor tile, byte[] bytes, DateTimeOffset cachedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (!configuration.CacheEnabled)
        {
            throw new InvalidOperationException("Map tile cache is disabled.");
        }

        var path = GetLocalCachePath(tile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        File.SetLastWriteTimeUtc(path, cachedAtUtc.UtcDateTime);
        EnforceMaximumCacheSize();

        return GetTileInfo(tile, cachedAtUtc);
    }

    public byte[]? ReadTile(MapTileDescriptor tile)
    {
        var path = GetLocalCachePath(tile);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public bool DeleteTile(MapTileDescriptor tile)
    {
        var path = GetLocalCachePath(tile);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public void Clear()
    {
        if (Directory.Exists(configuration.CacheRootFolder))
        {
            Directory.Delete(configuration.CacheRootFolder, recursive: true);
        }
    }

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(configuration.CacheRootFolder))
        {
            return 0;
        }

        return Directory.EnumerateFiles(configuration.CacheRootFolder, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
    }

    public void EnforceMaximumCacheSize()
    {
        if (configuration.MaximumCacheSizeBytes <= 0 || !Directory.Exists(configuration.CacheRootFolder))
        {
            return;
        }

        var files = Directory.EnumerateFiles(configuration.CacheRootFolder, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToList();
        var size = files.Sum(file => file.Length);

        foreach (var file in files)
        {
            if (size <= configuration.MaximumCacheSizeBytes)
            {
                break;
            }

            size -= file.Length;
            file.Delete();
        }
    }

    private static void ValidateTile(MapTileDescriptor tile)
    {
        if (string.IsNullOrWhiteSpace(tile.ProviderName))
        {
            throw new ArgumentException("Map provider name is required.", nameof(tile));
        }

        if (tile.ZoomLevel is < 0 or > MaximumZoomLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(tile), "Tile zoom level is outside the supported range.");
        }

        var maximumCoordinate = (1L << tile.ZoomLevel) - 1;
        if (tile.TileX < 0 || tile.TileY < 0 || tile.TileX > maximumCoordinate || tile.TileY > maximumCoordinate)
        {
            throw new ArgumentOutOfRangeException(nameof(tile), "Tile coordinates are outside the valid range for the zoom level.");
        }
    }

    private static string SanitizeProviderName(string providerName)
    {
        var builder = new StringBuilder(providerName.Length);
        foreach (var character in providerName.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsWhiteSpace(character))
            {
                builder.Append('-');
            }
        }

        var sanitized = builder.ToString().Trim('-', '.', '_');
        if (sanitized.Length == 0 || sanitized is "." or "..")
        {
            throw new ArgumentException("Map provider name does not contain safe path characters.", nameof(providerName));
        }

        return sanitized;
    }
}
