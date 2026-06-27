using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using System.Net.Http;

namespace Aprs.Desktop.Mapping;

/// <summary>
/// A BruTile tile source that fetches NOAA NEXRAD radar composite tiles from
/// the NWS GeoServer WMS endpoint (opengeo.ncep.noaa.gov). Returns transparent
/// PNG tiles in EPSG:3857 (Web Mercator) so they overlay correctly on Mapsui maps.
///
/// <para>The NOAA WMS API is a free public service — no API key required.</para>
/// </summary>
public sealed class WmsRadarTileSource : ITileSource
{
    private static readonly HttpClient SharedClient = CreateHttpClient();

    // Use the CONUS (contiguous US) base reflectivity layer.
    private const string WmsBaseUrl = "https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows";
    private const string LayerName  = "conus_bref_qcd";

    public ITileSchema Schema { get; }
    public string Name { get; } = "NEXRAD Radar";
    public Attribution Attribution { get; } = new Attribution(
        "NOAA/NWS NEXRAD", "https://www.weather.gov/");

    // In-memory only — radar data expires quickly, don't persist to disk.
    private readonly MemoryCache<byte[]> cache = new(100);

    public WmsRadarTileSource()
    {
        Schema = new GlobalSphericalMercator(0, 10) { Name = "NEXRAD Radar" };
    }

    public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        // Check in-memory cache first.
        var cached = cache.Find(tileInfo.Index);
        if (cached is not null) return cached;

        // Build the WMS GetMap URL from the tile's bounding box.
        var extent = tileInfo.Extent;
        var url = BuildWmsUrl(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY);

        try
        {
            var bytes = await SharedClient.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length > 512) // ignore tiny error responses
            {
                cache.Add(tileInfo.Index, bytes);
            }
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Clears the tile cache so new tiles are fetched on next render — call on radar refresh.</summary>
    public void InvalidateCache() => cache.Clear();

    private static string BuildWmsUrl(double minX, double minY, double maxX, double maxY)
        => $"{WmsBaseUrl}?" +
           $"service=WMS&version=1.3.0&request=GetMap" +
           $"&layers={LayerName}" +
           $"&bbox={minX:F2},{minY:F2},{maxX:F2},{maxY:F2}" +
           $"&width=256&height=256" +
           $"&crs=EPSG:3857" +
           $"&format=image%2Fpng" +
           $"&transparent=true" +
           $"&styles=";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.2.0 (github.com/KE4CON/APRS-Command)");
        return client;
    }
}
