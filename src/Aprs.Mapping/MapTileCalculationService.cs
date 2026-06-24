namespace Aprs.Mapping;

public sealed class MapTileCalculationService : IMapTileCalculationService
{
    public const int DefaultLargeDownloadTileThreshold = 10_000;
    public const long DefaultEstimatedTileSizeBytes = 25_000;

    public OfflineMapDownloadEstimate EstimateTiles(OfflineMapDownloadArea area, bool allowLargeArea = false)
    {
        area.Validate(allowLargeArea);

        var provider = new TemplateMapTileProvider(area.SelectedMapProvider);
        var ranges = new List<MapTileRange>();
        var tiles = new List<MapTileDescriptor>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        for (var zoom = area.MinimumZoom; zoom <= area.MaximumZoom; zoom++)
        {
            var (westX, northY) = ToTile(area.NorthLatitude, area.WestLongitude, zoom);
            var (eastX, southY) = ToTile(area.SouthLatitude, area.EastLongitude, zoom);
            var minX = Math.Min(westX, eastX);
            var maxX = Math.Max(westX, eastX);
            var minY = Math.Min(northY, southY);
            var maxY = Math.Max(northY, southY);
            ranges.Add(new MapTileRange(zoom, minX, maxX, minY, maxY));

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    var key = $"{zoom}/{x}/{y}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    tiles.Add(new MapTileDescriptor(
                        area.SelectedMapProvider.Name,
                        zoom,
                        x,
                        y,
                        provider.BuildTileUrl(zoom, x, y)));
                }
            }
        }

        var isLarge = tiles.Count > DefaultLargeDownloadTileThreshold;
        if (isLarge)
        {
            warnings.Add("Offline map download is large and should require explicit confirmation.");
        }

        if (!area.SelectedMapProvider.SupportsOfflineCaching)
        {
            warnings.Add("Selected map provider does not allow offline caching.");
        }

        if (!area.SelectedMapProvider.InternetDownloadAllowed)
        {
            warnings.Add("Selected map provider has internet download disabled.");
        }

        return new OfflineMapDownloadEstimate(
            area,
            ranges,
            tiles,
            tiles.Count,
            tiles.Count * DefaultEstimatedTileSizeBytes,
            isLarge,
            warnings);
    }

    private static (int X, int Y) ToTile(double latitude, double longitude, int zoom)
    {
        var clampedLatitude = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var latitudeRadians = clampedLatitude * Math.PI / 180;
        var tileCount = 1 << zoom;
        var x = (int)Math.Floor((longitude + 180.0) / 360.0 * tileCount);
        var y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latitudeRadians) + 1.0 / Math.Cos(latitudeRadians)) / Math.PI) / 2.0 * tileCount);

        return (Math.Clamp(x, 0, tileCount - 1), Math.Clamp(y, 0, tileCount - 1));
    }
}
