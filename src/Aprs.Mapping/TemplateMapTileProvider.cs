namespace Aprs.Mapping;

public sealed class TemplateMapTileProvider : IMapTileProvider
{
    public TemplateMapTileProvider(MapTileProviderDefinition definition)
    {
        Definition = definition;
    }

    public MapTileProviderDefinition Definition { get; }

    public string BuildTileUrl(int zoomLevel, int tileX, int tileY)
    {
        if (zoomLevel < Definition.MinimumZoom || zoomLevel > Definition.MaximumZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomLevel), "Zoom level is outside the provider range.");
        }

        return Definition.UrlTemplate
            .Replace("{z}", zoomLevel.ToString(), StringComparison.Ordinal)
            .Replace("{x}", tileX.ToString(), StringComparison.Ordinal)
            .Replace("{y}", tileY.ToString(), StringComparison.Ordinal);
    }
}
