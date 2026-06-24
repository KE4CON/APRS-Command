namespace Aprs.Mapping;

public interface IMapTileDownloadClient
{
    /// <summary>
    /// Downloads one tile. Implementations may be fake/offline in tests.
    /// </summary>
    Task<byte[]> DownloadTileAsync(MapTileDescriptor tile, CancellationToken cancellationToken);
}
