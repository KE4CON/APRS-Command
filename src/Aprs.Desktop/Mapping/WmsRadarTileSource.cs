using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using System.Net.Http;

namespace Aprs.Desktop.Mapping;

/// <summary>
/// A BruTile tile source that fetches NOAA NEXRAD radar composite tiles from
/// the NWS GeoServer WMS endpoint (opengeo.ncep.noaa.gov). Returns transparent
/// PNG tiles in EPSG:3857 (Web Mercator) so they overlay correctly on Mapsui maps.
///
/// <para>Supports an optional ISO8601 timestamp parameter for animation frame
/// fetching. When timestamp is null, fetches the most recent (default) frame.</para>
///
/// <para>The NOAA WMS API is a free public service — no API key required.</para>
/// </summary>
public sealed class WmsRadarTileSource : ITileSource
{
    private static readonly HttpClient SharedClient = CreateHttpClient();

    private const string WmsBaseUrl = "https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows";
    private const string LayerName  = "conus_bref_qcd";

    public ITileSchema Schema { get; }
    public string Name { get; }
    public Attribution Attribution { get; } = new Attribution(
        "NOAA/NWS NEXRAD", "https://www.weather.gov/");

    /// <summary>ISO8601 timestamp for this frame, or null for the latest frame.</summary>
    public string? Timestamp { get; }

    // In-memory only — radar data expires quickly, don't persist to disk.
    private readonly MemoryCache<byte[]> cache = new(60);

    /// <param name="timestamp">ISO8601 timestamp e.g. "2026-06-28T16:00:00Z", or null for latest.</param>
    public WmsRadarTileSource(string? timestamp = null)
    {
        Timestamp = timestamp;
        Name      = timestamp is null ? "NEXRAD Radar (latest)" : $"NEXRAD Radar {timestamp}";
        Schema    = new GlobalSphericalMercator(0, 10) { Name = Name };
    }

    public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        var cached = cache.Find(tileInfo.Index);
        if (cached is not null) return cached;

        var extent = tileInfo.Extent;
        var url    = BuildWmsUrl(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY);

        try
        {
            var bytes = await SharedClient.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length > 512)
                cache.Add(tileInfo.Index, bytes);
            return bytes;
        }
        catch { return null; }
    }

    /// <summary>Clears the tile cache — call on radar refresh or when changing frames.</summary>
    public void InvalidateCache() => cache.Clear();

    private string BuildWmsUrl(double minX, double minY, double maxX, double maxY)
    {
        var timeParam = Timestamp is not null ? $"&time={Uri.EscapeDataString(Timestamp)}" : string.Empty;
        return $"{WmsBaseUrl}?" +
               $"service=WMS&version=1.3.0&request=GetMap" +
               $"&layers={LayerName}" +
               $"&bbox={minX:F2},{minY:F2},{maxX:F2},{maxY:F2}" +
               $"&width=256&height=256" +
               $"&crs=EPSG:3857" +
               $"&format=image%2Fpng" +
               $"&transparent=true" +
               $"&styles={timeParam}";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.3.0 (github.com/KE4CON/APRS-Command)");
        return client;
    }
}
