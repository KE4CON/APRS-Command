namespace Aprs.Mapping;

public sealed class OfflineMapDownloadManager : IOfflineMapDownloadManager
{
    private readonly IMapTileCalculationService tileCalculationService;
    private readonly IMapTileCacheService tileCacheService;
    private readonly IMapTileDownloadClient downloadClient;

    public OfflineMapDownloadManager(
        IMapTileCalculationService tileCalculationService,
        IMapTileCacheService tileCacheService,
        IMapTileDownloadClient downloadClient)
    {
        this.tileCalculationService = tileCalculationService;
        this.tileCacheService = tileCacheService;
        this.downloadClient = downloadClient;
    }

    public OfflineMapDownloadJob CreateJob(OfflineMapDownloadArea area)
    {
        area.Validate();
        return new OfflineMapDownloadJob(Guid.NewGuid(), area);
    }

    public OfflineMapDownloadEstimate EstimateJob(OfflineMapDownloadArea area, bool allowLargeArea = false)
    {
        return tileCalculationService.EstimateTiles(area, allowLargeArea);
    }

    public async Task<OfflineMapDownloadJob> StartJobAsync(
        OfflineMapDownloadJob job,
        OfflineMapDownloadArea area,
        bool allowLargeArea,
        CancellationToken cancellationToken)
    {
        if (!area.SelectedMapProvider.SupportsOfflineCaching)
        {
            job.CurrentStatus = OfflineMapDownloadStatus.Failed;
            job.Errors.Add("Selected provider does not allow offline caching.");
            return job;
        }

        if (!area.SelectedMapProvider.InternetDownloadAllowed)
        {
            job.CurrentStatus = OfflineMapDownloadStatus.Failed;
            job.Errors.Add("Selected provider does not allow internet tile downloads.");
            return job;
        }

        job.CurrentStatus = OfflineMapDownloadStatus.Estimating;
        OfflineMapDownloadEstimate estimate;
        try
        {
            estimate = EstimateJob(area, allowLargeArea);
        }
        catch (Exception exception)
        {
            job.CurrentStatus = OfflineMapDownloadStatus.Failed;
            job.Errors.Add(exception.Message);
            return job;
        }

        job.TotalTiles = estimate.TotalTiles;
        job.StartedAtUtc = DateTimeOffset.UtcNow;
        job.CurrentStatus = OfflineMapDownloadStatus.Downloading;

        foreach (var tile in estimate.Tiles)
        {
            if (job.CancellationRequested || cancellationToken.IsCancellationRequested)
            {
                job.CurrentStatus = OfflineMapDownloadStatus.Cancelled;
                job.CompletedAtUtc = DateTimeOffset.UtcNow;
                return job;
            }

            if (tileCacheService.TileExists(tile))
            {
                job.SkippedExistingTiles++;
                continue;
            }

            try
            {
                var bytes = await downloadClient.DownloadTileAsync(tile, cancellationToken).ConfigureAwait(false);
                tileCacheService.SaveTile(tile, bytes, DateTimeOffset.UtcNow);
                job.CompletedTiles++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                job.FailedTiles++;
                job.Errors.Add($"{tile.ZoomLevel}/{tile.TileX}/{tile.TileY}: {exception.Message}");
            }
        }

        job.CompletedAtUtc = DateTimeOffset.UtcNow;
        job.CurrentStatus = job.FailedTiles > 0
            ? OfflineMapDownloadStatus.Failed
            : OfflineMapDownloadStatus.Completed;
        return job;
    }

    public void CancelJob(OfflineMapDownloadJob job)
    {
        job.CancellationRequested = true;
    }
}
