namespace Aprs.Mapping;

public sealed class OfflineMapDownloadJob
{
    public OfflineMapDownloadJob(Guid jobId, OfflineMapDownloadArea area)
    {
        JobId = jobId;
        AreaName = area.AreaName;
        Provider = area.SelectedMapProvider;
        MinimumZoom = area.MinimumZoom;
        MaximumZoom = area.MaximumZoom;
    }

    public Guid JobId { get; }

    public string AreaName { get; }

    public MapTileProviderDefinition Provider { get; }

    public int MinimumZoom { get; }

    public int MaximumZoom { get; }

    public long TotalTiles { get; internal set; }

    public long CompletedTiles { get; internal set; }

    public long FailedTiles { get; internal set; }

    public long SkippedExistingTiles { get; internal set; }

    public OfflineMapDownloadStatus CurrentStatus { get; internal set; } = OfflineMapDownloadStatus.Pending;

    public double ProgressPercentage => TotalTiles == 0
        ? 0
        : Math.Round((CompletedTiles + FailedTiles + SkippedExistingTiles) * 100.0 / TotalTiles, 2);

    public DateTimeOffset? StartedAtUtc { get; internal set; }

    public DateTimeOffset? CompletedAtUtc { get; internal set; }

    public bool CancellationRequested { get; internal set; }

    public List<string> Errors { get; } = [];
}
