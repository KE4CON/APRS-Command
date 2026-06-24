using Aprs.Mapping;
using Xunit;

namespace Aprs.Tests;

public sealed class MapTileCacheServiceTests : IDisposable
{
    private readonly string cacheRoot = Path.Combine(Path.GetTempPath(), "APRSCommandTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(cacheRoot))
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void GenerateCacheKey_UsesStableSafeTileKey()
    {
        var service = CreateService();

        var key = service.GenerateCacheKey(CreateTile(providerName: "Sample Provider"));

        Assert.Equal("sample-provider/3/4/2.tile", key);
    }

    [Fact]
    public void GetLocalCachePath_SanitizesProviderNameAndStaysUnderRoot()
    {
        var service = CreateService();

        var path = service.GetLocalCachePath(CreateTile(providerName: "../Bad Provider?"));

        Assert.StartsWith(Path.GetFullPath(cacheRoot), path, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("bad-provider", "3", "4", "2.tile"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void TileExists_ReturnsFalseForMissingTile()
    {
        var service = CreateService();

        Assert.False(service.TileExists(CreateTile()));
        Assert.Null(service.ReadTile(CreateTile()));
    }

    [Fact]
    public void SaveTile_StoresTileAndReadTileReturnsSameBytes()
    {
        var service = CreateService();
        var tile = CreateTile();
        var bytes = new byte[] { 1, 2, 3, 4 };

        var entry = service.SaveTile(tile, bytes, DateTimeOffset.UtcNow);
        var read = service.ReadTile(tile);

        Assert.True(entry.ExistsInCache);
        Assert.Equal(bytes.Length, entry.TileSizeBytes);
        Assert.Equal(bytes, read);
        Assert.True(File.Exists(entry.LocalCachePath));
    }

    [Fact]
    public void GetTileInfo_IncludesExpirationWhenConfigured()
    {
        var cachedAt = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(tileExpirationAge: TimeSpan.FromDays(7));
        var tile = CreateTile();

        var entry = service.SaveTile(tile, [1, 2, 3], cachedAt);

        Assert.Equal(cachedAt.AddDays(7), entry.ExpiresAtUtc);
        Assert.True(entry.ExistsInCache);
    }

    [Fact]
    public void DeleteTile_RemovesOneTile()
    {
        var service = CreateService();
        var tile = CreateTile();
        service.SaveTile(tile, [1, 2, 3], DateTimeOffset.UtcNow);

        var deleted = service.DeleteTile(tile);

        Assert.True(deleted);
        Assert.False(service.TileExists(tile));
        Assert.False(service.DeleteTile(tile));
    }

    [Fact]
    public void Clear_RemovesAllCachedTiles()
    {
        var service = CreateService();
        service.SaveTile(CreateTile(tileX: 1), [1, 2], DateTimeOffset.UtcNow);
        service.SaveTile(CreateTile(tileX: 2), [3, 4], DateTimeOffset.UtcNow);

        service.Clear();

        Assert.Equal(0, service.GetCacheSizeBytes());
        Assert.False(Directory.Exists(cacheRoot));
    }

    [Fact]
    public void GetCacheSizeBytes_ReturnsApproximateCacheSize()
    {
        var service = CreateService();
        service.SaveTile(CreateTile(tileX: 1), [1, 2, 3], DateTimeOffset.UtcNow);
        service.SaveTile(CreateTile(tileX: 2), [4, 5], DateTimeOffset.UtcNow);

        Assert.Equal(5, service.GetCacheSizeBytes());
    }

    [Fact]
    public void EnforceMaximumCacheSize_RemovesOldestTiles()
    {
        var service = CreateService(maximumCacheSizeBytes: 5);
        service.SaveTile(CreateTile(tileX: 1), [1, 2, 3, 4], new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        service.SaveTile(CreateTile(tileX: 2), [5, 6, 7, 8], new DateTimeOffset(2026, 6, 10, 12, 1, 0, TimeSpan.Zero));

        Assert.True(service.GetCacheSizeBytes() <= 5);
        Assert.False(service.TileExists(CreateTile(tileX: 1)));
        Assert.True(service.TileExists(CreateTile(tileX: 2)));
    }

    [Fact]
    public void ProviderMetadata_IsStoredAndBuildsTileUrl()
    {
        var definition = new MapTileProviderDefinition(
            "Example",
            "https://tiles.example.test/{z}/{x}/{y}.png",
            MinimumZoom: 1,
            MaximumZoom: 12,
            AttributionText: "Example attribution",
            SupportsOfflineCaching: true,
            InternetDownloadAllowed: false);
        var provider = new TemplateMapTileProvider(definition);

        var url = provider.BuildTileUrl(3, 4, 2);

        Assert.Equal("Example", provider.Definition.Name);
        Assert.Equal("Example attribution", provider.Definition.AttributionText);
        Assert.True(provider.Definition.SupportsOfflineCaching);
        Assert.False(provider.Definition.InternetDownloadAllowed);
        Assert.Equal("https://tiles.example.test/3/4/2.png", url);
    }

    [Fact]
    public void InvalidTileCoordinatesOrProviderNames_AreRejected()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.GenerateCacheKey(CreateTile(providerName: "///")));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GenerateCacheKey(CreateTile(zoomLevel: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GenerateCacheKey(CreateTile(zoomLevel: 2, tileX: 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GenerateCacheKey(CreateTile(zoomLevel: 2, tileY: 4)));
    }

    private MapTileCacheService CreateService(
        long maximumCacheSizeBytes = 1024 * 1024,
        TimeSpan? tileExpirationAge = null)
    {
        var configuration = new MapTileCacheConfiguration(
            CacheEnabled: true,
            CacheRootFolder: cacheRoot,
            MaximumCacheSizeBytes: maximumCacheSizeBytes,
            TileExpirationAge: tileExpirationAge,
            ProviderName: "SampleGrid",
            AllowInternetTileDownload: false);

        return new MapTileCacheService(configuration);
    }

    private static MapTileDescriptor CreateTile(
        string providerName = "SampleGrid",
        int zoomLevel = 3,
        int tileX = 4,
        int tileY = 2)
    {
        return new MapTileDescriptor(
            providerName,
            zoomLevel,
            tileX,
            tileY,
            $"https://tiles.example.test/{zoomLevel}/{tileX}/{tileY}.png");
    }
}
