namespace Aprs.Mapping;

public enum OfflineMapDownloadStatus
{
    Pending,
    Estimating,
    Downloading,
    Paused,
    Cancelled,
    Completed,
    Failed
}
