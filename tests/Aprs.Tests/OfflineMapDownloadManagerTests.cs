using Aprs.Mapping;
using Xunit;

namespace Aprs.Tests;

public sealed class OfflineMapDownloadManagerTests : IDisposable
{
    private readonly string cacheRoot = Path.Combine(Path.GetTempPath(), "APRSCommandDownloadTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(cacheRoot))
        {
            Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void EstimateTiles_ConvertsBoundingBoxToTileRanges()
    {
        var estimate = new MapTileCalculationService().EstimateTiles(CreateArea(minZoom: 3, maxZoom: 3));

        var range = Assert.Single(estimate.TileRanges);
        Assert.Equal(3, range.ZoomLevel);
        Assert.True(range.MinimumTileX <= range.MaximumTileX);
        Assert.True(range.MinimumTileY <= range.MaximumTileY);
        Assert.Equal(range.TileCount, estimate.TotalTiles);
        Assert.Equal(estimate.TotalTiles, estimate.Tiles.DistinctBy(tile => $"{tile.ZoomLevel}/{tile.TileX}/{tile.TileY}").Count());
    }

    [Fact]
    public void EstimateTiles_CalculatesTileCountAndSize()
    {
        var estimate = new MapTileCalculationService().EstimateTiles(CreateArea(minZoom: 3, maxZoom: 4));

        Assert.True(estimate.TotalTiles > 0);
        Assert.Equal(estimate.TotalTiles * MapTileCalculationService.DefaultEstimatedTileSizeBytes, estimate.EstimatedSizeBytes);
    }

    [Fact]
    public void EstimateTiles_RejectsInvalidBoundingBoxes()
    {
        var area = CreateArea(north: 38, south: 39);

        Assert.Throws<ArgumentException>(() => new MapTileCalculationService().EstimateTiles(area));
    }

    [Fact]
    public void EstimateTiles_RejectsInvalidZoomRange()
    {
        var area = CreateArea(minZoom: 10, maxZoom: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MapTileCalculationService().EstimateTiles(area));
    }

    [Fact]
    public void EstimateTiles_RejectsOversizedAreaUnlessAllowed()
    {
        var area = CreateArea(north: 80, south: -80, west: -170, east: 170, minZoom: 1, maxZoom: 1);
        var service = new MapTileCalculationService();

        Assert.Throws<InvalidOperationException>(() => service.EstimateTiles(area));
        var estimate = service.EstimateTiles(area, allowLargeArea: true);

        Assert.True(estimate.TotalTiles > 0);
    }

    [Fact]
    public async Task StartJobAsync_UpdatesProgressAndSavesTiles()
    {
        var cache = CreateCache();
        var manager = CreateManager(cache, new FakeTileDownloadClient(_ => [1, 2, 3]));
        var area = CreateArea(north: 40, south: 38, west: -85, east: -83, minZoom: 8, maxZoom: 8);
        var job = manager.CreateJob(area);

        var completed = await manager.StartJobAsync(job, area, allowLargeArea: false, CancellationToken.None);

        Assert.Equal(OfflineMapDownloadStatus.Completed, completed.CurrentStatus);
        Assert.True(completed.CompletedTiles > 0);
        Assert.Equal(completed.TotalTiles, completed.CompletedTiles);
        Assert.Equal(100, completed.ProgressPercentage);
        Assert.True(cache.GetCacheSizeBytes() > 0);
    }

    [Fact]
    public async Task StartJobAsync_SkipsExistingCachedTiles()
    {
        var cache = CreateCache();
        var area = CreateArea(minZoom: 3, maxZoom: 4);
        var estimate = new MapTileCalculationService().EstimateTiles(area);
        cache.SaveTile(estimate.Tiles.First(), [9, 9, 9], DateTimeOffset.UtcNow);
        var manager = CreateManager(cache, new FakeTileDownloadClient(_ => [1, 2, 3]));
        var job = manager.CreateJob(area);

        var completed = await manager.StartJobAsync(job, area, allowLargeArea: false, CancellationToken.None);

        Assert.Equal(1, completed.SkippedExistingTiles);
        Assert.Equal(completed.TotalTiles, completed.CompletedTiles + completed.SkippedExistingTiles);
    }

    [Fact]
    public async Task StartJobAsync_HandlesCancellationSafely()
    {
        var cache = CreateCache();
        var manager = CreateManager(cache, new FakeTileDownloadClient(_ => [1]));
        var area = CreateArea(minZoom: 3, maxZoom: 4);
        var job = manager.CreateJob(area);
        var cancellingClient = new CancellingTileDownloadClient(() => manager.CancelJob(job));
        manager = CreateManager(cache, cancellingClient);

        var completed = await manager.StartJobAsync(job, area, allowLargeArea: false, CancellationToken.None);

        Assert.Equal(OfflineMapDownloadStatus.Cancelled, completed.CurrentStatus);
        Assert.True(completed.CancellationRequested);
    }

    [Fact]
    public async Task StartJobAsync_ProviderDownloadDisabledBlocksDownload()
    {
        var area = CreateArea(provider: CreateProvider(internetDownloadAllowed: false));
        var manager = CreateManager(CreateCache(), new FakeTileDownloadClient(_ => [1]));
        var job = manager.CreateJob(area);

        var completed = await manager.StartJobAsync(job, area, allowLargeArea: false, CancellationToken.None);

        Assert.Equal(OfflineMapDownloadStatus.Failed, completed.CurrentStatus);
        Assert.Contains(completed.Errors, error => error.Contains("does not allow internet tile downloads", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartJobAsync_FailedTileDownloadDoesNotCrashWholeJob()
    {
        var cache = CreateCache();
        var manager = CreateManager(cache, new FailingFirstTileDownloadClient());
        var area = CreateArea(north: 40, south: 38, west: -85, east: -83, minZoom: 8, maxZoom: 8);
        var job = manager.CreateJob(area);

        var completed = await manager.StartJobAsync(job, area, allowLargeArea: false, CancellationToken.None);

        Assert.Equal(OfflineMapDownloadStatus.Failed, completed.CurrentStatus);
        Assert.Equal(1, completed.FailedTiles);
        Assert.NotEmpty(completed.Errors);
        Assert.True(completed.CompletedTiles > 0);
    }

    private OfflineMapDownloadManager CreateManager(IMapTileCacheService cache, IMapTileDownloadClient downloadClient)
    {
        return new OfflineMapDownloadManager(new MapTileCalculationService(), cache, downloadClient);
    }

    private MapTileCacheService CreateCache()
    {
        return new MapTileCacheService(new MapTileCacheConfiguration(
            CacheEnabled: true,
            CacheRootFolder: cacheRoot,
            MaximumCacheSizeBytes: 1024 * 1024,
            TileExpirationAge: TimeSpan.FromDays(30),
            ProviderName: "TestProvider",
            AllowInternetTileDownload: true));
    }

    private static OfflineMapDownloadArea CreateArea(
        double north = 39.2,
        double south = 39.0,
        double west = -84.7,
        double east = -84.4,
        int minZoom = 3,
        int maxZoom = 3,
        MapTileProviderDefinition? provider = null)
    {
        var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
        return new OfflineMapDownloadArea(
            "Test area",
            north,
            south,
            east,
            west,
            minZoom,
            maxZoom,
            provider ?? CreateProvider(),
            now,
            now,
            "Offline test area");
    }

    private static MapTileProviderDefinition CreateProvider(
        bool supportsOfflineCaching = true,
        bool internetDownloadAllowed = true)
    {
        return new MapTileProviderDefinition(
            "TestProvider",
            "https://tiles.example.test/{z}/{x}/{y}.png",
            MinimumZoom: 0,
            MaximumZoom: 18,
            AttributionText: "Example attribution",
            SupportsOfflineCaching: supportsOfflineCaching,
            InternetDownloadAllowed: internetDownloadAllowed);
    }

    private sealed class FakeTileDownloadClient : IMapTileDownloadClient
    {
        private readonly Func<MapTileDescriptor, byte[]> factory;

        public FakeTileDownloadClient(Func<MapTileDescriptor, byte[]> factory)
        {
            this.factory = factory;
        }

        public Task<byte[]> DownloadTileAsync(MapTileDescriptor tile, CancellationToken cancellationToken)
        {
            return Task.FromResult(factory(tile));
        }
    }

    private sealed class CancellingTileDownloadClient : IMapTileDownloadClient
    {
        private readonly Action? onDownload;

        public CancellingTileDownloadClient(Action? onDownload)
        {
            this.onDownload = onDownload;
        }

        public Task<byte[]> DownloadTileAsync(MapTileDescriptor tile, CancellationToken cancellationToken)
        {
            onDownload?.Invoke();
            return Task.FromResult(new byte[] { 1 });
        }
    }

    private sealed class FailingFirstTileDownloadClient : IMapTileDownloadClient
    {
        private bool failed;

        public Task<byte[]> DownloadTileAsync(MapTileDescriptor tile, CancellationToken cancellationToken)
        {
            if (!failed)
            {
                failed = true;
                throw new InvalidOperationException("Simulated tile failure.");
            }

            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }
}
