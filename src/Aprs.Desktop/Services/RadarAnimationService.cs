using System.Net.Http;
using System.Xml.Linq;

namespace Aprs.Desktop.Services;

/// <summary>A single animation frame — a timestamp and its display label.</summary>
public sealed record RadarFrame(string Timestamp, DateTimeOffset Time)
{
    public string Label => Time.ToLocalTime().ToString("HH:mm");
}

/// <summary>
/// Fetches the available NEXRAD radar timestamps from the NOAA WMS GetCapabilities
/// endpoint and manages the animation frame list. Exposes the ordered list of frames
/// so the animation engine can request tiles for each one.
///
/// <para>NOAA typically exposes ~60 frames at 2-minute intervals covering the last
/// 2 hours. We use the most recent 10 frames for animation to keep tile count
/// manageable while still showing meaningful storm movement.</para>
/// </summary>
public sealed class RadarAnimationService : IAsyncDisposable
{
    private const string CapabilitiesUrl =
        "https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows" +
        "?service=WMS&request=GetCapabilities";

    private const int MaxFrames = 10;

    private readonly HttpClient http;
    private readonly CancellationTokenSource cts = new();

    /// <summary>Available animation frames ordered oldest → newest.</summary>
    public IReadOnlyList<RadarFrame> Frames { get; private set; } = [];

    /// <summary>Fired when a new set of frames has been fetched.</summary>
    public event EventHandler<IReadOnlyList<RadarFrame>>? FramesRefreshed;

    public RadarAnimationService()
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.3.0 (github.com/KE4CON/APRS-Command)");
    }

    /// <summary>
    /// Fetches the current list of available radar timestamps from the WMS
    /// GetCapabilities endpoint. Returns the most recent MaxFrames timestamps
    /// ordered oldest → newest for animation playback.
    /// </summary>
    public async Task RefreshFramesAsync()
    {
        try
        {
            var xml      = await http.GetStringAsync(CapabilitiesUrl, cts.Token)
                                     .ConfigureAwait(false);
            var doc      = XDocument.Parse(xml);
            var ns       = XNamespace.Get("http://www.opengis.net/wms");
            var dimEl    = doc.Descendants(ns + "Dimension")
                              .FirstOrDefault(d => (string?)d.Attribute("name") == "time");

            if (dimEl?.Value is null) return;

            var timestamps = dimEl.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => DateTimeOffset.TryParse(t, out _))
                .Select(t => new RadarFrame(t, DateTimeOffset.Parse(t)))
                .OrderBy(f => f.Time)
                .TakeLast(MaxFrames)
                .ToList();

            if (timestamps.Count == 0) return;

            Frames = timestamps;
            FramesRefreshed?.Invoke(this, timestamps);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RadarAnimDebug] RefreshFramesAsync FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        http.Dispose();
        cts.Dispose();
    }
}
