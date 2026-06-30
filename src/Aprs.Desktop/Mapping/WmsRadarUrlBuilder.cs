using BruTile;
using BruTile.Web;

namespace Aprs.Desktop.Mapping;

/// <summary>
/// Builds request URLs for NOAA NEXRAD radar composite tiles from the NWS
/// GeoServer WMS endpoint (opengeo.ncep.noaa.gov). Used with BruTile's
/// HttpTileSource, which is the supported integration point for custom HTTP
/// tile fetching in Mapsui — TileLayer's internal fetch pipeline specifically
/// requires either an IHttpTileSource (i.e. HttpTileSource) or an
/// ILocalTileSource (an internal Mapsui.Tiling interface this project cannot
/// implement). A bespoke ITileSource implementation is silently never invoked.
///
/// <para>Supports an optional ISO8601 timestamp parameter for animation frame
/// fetching. When timestamp is null, fetches the most recent (default) frame.</para>
///
/// <para>The NOAA WMS API is a free public service — no API key required.</para>
/// </summary>
public sealed class WmsRadarUrlBuilder : IUrlBuilder
{
    private const string WmsBaseUrl = "https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows";
    private const string LayerName  = "conus_bref_qcd";

    /// <summary>ISO8601 timestamp for this frame, or null for the latest frame.</summary>
    public string? Timestamp { get; }

    /// <param name="timestamp">ISO8601 timestamp e.g. "2026-06-28T16:00:00Z", or null for latest.</param>
    public WmsRadarUrlBuilder(string? timestamp = null) => Timestamp = timestamp;

    public Uri GetUrl(TileInfo tileInfo)
    {
        var extent    = tileInfo.Extent;
        var timeParam = Timestamp is not null ? $"&time={Uri.EscapeDataString(Timestamp)}" : string.Empty;
        var url = $"{WmsBaseUrl}?" +
                  $"service=WMS&version=1.3.0&request=GetMap" +
                  $"&layers={LayerName}" +
                  $"&bbox={extent.MinX:F2},{extent.MinY:F2},{extent.MaxX:F2},{extent.MaxY:F2}" +
                  $"&width=256&height=256" +
                  $"&crs=EPSG:3857" +
                  $"&format=image%2Fpng" +
                  $"&transparent=true" +
                  $"&styles={timeParam}";
        return new Uri(url);
    }
}
