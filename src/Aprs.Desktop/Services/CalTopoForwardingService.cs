using System.Collections.Concurrent;
using System.Net.Http;
using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.Services;

/// <summary>
/// Forwards received APRS station positions to a CalTopo / SARTopo map
/// in real time using the CalTopo position reporting endpoint.
///
/// <para>Endpoint used:</para>
/// <code>
/// POST https://caltopo.com/api/v1/position/report/other
///      ?id={callsign}&amp;lat={lat}&amp;lng={lon}&amp;mapId={mapId}
/// </code>
///
/// <para>No CalTopo account, API key, or authentication is required.
/// A Locator object must be added to the CalTopo map before positions
/// will appear (Add → Locator → Live Track – Fleet, Email, Other).</para>
///
/// <para>Rate limiting: each callsign is throttled to one update per
/// <see cref="CalTopoSettings.MinimumIntervalSeconds"/> to avoid
/// flooding CalTopo with updates. The default is 60 seconds.</para>
///
/// <para>Failures are silent — if CalTopo is unreachable (common in
/// field deployments without internet), forwarding stops retrying and
/// resumes when the next packet arrives. This never affects APRS
/// Command's own packet processing.</para>
/// </summary>
public sealed class CalTopoForwardingService : IAsyncDisposable
{
    private const string BaseUrl =
        "https://caltopo.com/api/v1/position/report/other";

    private readonly HttpClient http;
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastSent = new();
    private CalTopoSettings settings;

    public CalTopoForwardingService(CalTopoSettings settings)
    {
        this.settings = settings;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(AppVersion.UserAgent);
    }

    /// <summary>
    /// Updates the active settings. Call this when the user saves new
    /// CalTopo configuration without restarting the application.
    /// </summary>
    public void ApplySettings(CalTopoSettings updated) => settings = updated;

    /// <summary>
    /// Handles a parsed APRS packet event. Filters to position packets,
    /// applies per-callsign rate limiting, and POSTs to CalTopo.
    /// </summary>
    public void OnPacketParsed(object? sender, ParsedPacketEventArgs e)
    {
        if (!settings.Enabled
            || string.IsNullOrWhiteSpace(settings.MapId)
            || e.Packet is not PositionAprsPacket pos
            || pos.Latitude is null
            || pos.Longitude is null
            || !pos.IsValid)
        {
            return;
        }

        // Filter by source
        if (e.Source == AprsPacketSource.AprsIs && !settings.ForwardAprsIsPackets) return;
        if (e.Source is AprsPacketSource.TcpKiss
                     or AprsPacketSource.SerialKiss
                     or AprsPacketSource.Agwpe
            && !settings.ForwardRfPackets) return;

        // Per-callsign rate limiting
        var callsign = string.IsNullOrEmpty(pos.SourceCallsign)
            ? "UNKNOWN"
            : pos.SourceSsid.HasValue
                ? $"{pos.SourceCallsign}-{pos.SourceSsid}"
                : pos.SourceCallsign;

        var minInterval = TimeSpan.FromSeconds(
            Math.Max(10, settings.MinimumIntervalSeconds));

        var now = DateTimeOffset.UtcNow;
        if (lastSent.TryGetValue(callsign, out var last)
            && now - last < minInterval)
        {
            return; // too soon for this callsign
        }

        lastSent[callsign] = now;

        // Fire and forget — failures must never propagate to the caller
        _ = SendAsync(callsign, pos.Latitude.Value, pos.Longitude.Value,
                      settings.MapId);
    }

    private async Task SendAsync(
        string callsign, double lat, double lon, string mapId)
    {
        try
        {
            var url = $"{BaseUrl}" +
                      $"?id={Uri.EscapeDataString(callsign)}" +
                      $"&lat={lat:F6}" +
                      $"&lng={lon:F6}" +
                      $"&mapId={Uri.EscapeDataString(mapId)}";

            using var response = await http.PostAsync(url, null).ConfigureAwait(false);
            // CalTopo returns {"result":{},"status":"ok","timestamp":...} on success.
            // We don't need to parse it — if the POST succeeded, we're done.
        }
        catch
        {
            // Silent failure — offline field deployments are the primary use case.
            // The next packet for this callsign will trigger a new attempt.
            lastSent.TryRemove(callsign, out _); // allow immediate retry next time
        }
    }

    /// <summary>
    /// Returns the number of unique callsigns that have been forwarded
    /// in the current session. Useful for displaying forwarding status.
    /// </summary>
    public int ForwardedStationCount => lastSent.Count;

    /// <summary>
    /// Clears the per-callsign rate limit cache. Useful when the user
    /// changes the CalTopo map ID mid-session.
    /// </summary>
    public void ResetRateLimits() => lastSent.Clear();

    public ValueTask DisposeAsync()
    {
        http.Dispose();
        return ValueTask.CompletedTask;
    }
}
