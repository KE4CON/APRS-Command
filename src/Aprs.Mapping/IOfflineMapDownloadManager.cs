namespace Aprs.Mapping;

public interface IOfflineMapDownloadManager
{
    /// <summary>
    /// Creates a pending offline map download job.
    /// </summary>
    OfflineMapDownloadJob CreateJob(OfflineMapDownloadArea area);

    /// <summary>
    /// Estimates a job before downloading tiles.
    /// </summary>
    OfflineMapDownloadEstimate EstimateJob(OfflineMapDownloadArea area, bool allowLargeArea = false);

    /// <summary>
    /// Starts downloading tiles for a job.
    /// </summary>
    Task<OfflineMapDownloadJob> StartJobAsync(
        OfflineMapDownloadJob job,
        OfflineMapDownloadArea area,
        bool allowLargeArea,
        CancellationToken cancellationToken);

    /// <summary>
    /// Requests cancellation for a job.
    /// </summary>
    void CancelJob(OfflineMapDownloadJob job);
}
