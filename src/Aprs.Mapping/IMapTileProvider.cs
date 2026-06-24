namespace Aprs.Mapping;

public interface IMapTileProvider
{
    /// <summary>
    /// Gets metadata for a tile provider without initiating network access.
    /// </summary>
    MapTileProviderDefinition Definition { get; }

    /// <summary>
    /// Builds a tile URL from provider metadata and tile coordinates.
    /// </summary>
    string BuildTileUrl(int zoomLevel, int tileX, int tileY);
}
