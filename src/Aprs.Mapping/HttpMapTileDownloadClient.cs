using System.Net.Http;

namespace Aprs.Mapping;

/// <summary>
/// Downloads map tiles from their provider URL using HTTP.
/// Uses the TileUrl already computed on the MapTileDescriptor.
/// </summary>
public sealed class HttpMapTileDownloadClient : IMapTileDownloadClient, IDisposable
{
    private readonly HttpClient http;

    public HttpMapTileDownloadClient()
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var version = typeof(HttpMapTileDownloadClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"APRSCommand/{version} (github.com/KE4CON/APRS-Command; offline-map-download)");
    }

    public async Task<byte[]> DownloadTileAsync(MapTileDescriptor tile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tile.TileUrl))
            throw new InvalidOperationException($"Tile has no URL: {tile.ProviderName} z{tile.ZoomLevel}/{tile.TileX}/{tile.TileY}");

        return await http.GetByteArrayAsync(tile.TileUrl, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => http.Dispose();
}
