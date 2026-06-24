namespace Aprs.Mapping;

public interface IMapTileCalculationService
{
    /// <summary>
    /// Calculates tile ranges and estimated tile count for an offline map area.
    /// </summary>
    OfflineMapDownloadEstimate EstimateTiles(OfflineMapDownloadArea area, bool allowLargeArea = false);
}
