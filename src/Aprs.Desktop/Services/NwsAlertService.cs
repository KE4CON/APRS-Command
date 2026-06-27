using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aprs.Desktop.Services;

/// <summary>
/// Fetches active NWS weather alerts for the operator's location using the
/// api.weather.gov REST API. No API key required — the NWS provides this
/// as a free public service.
///
/// <para>Alerts are fetched by lat/lon point, which returns all active alerts
/// for that location from the NWS. Results are cached and refreshed on a
/// configurable interval (default 5 minutes).</para>
/// </summary>
public sealed class NwsAlertService : IAsyncDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource cts = new();
    private Task? pollLoop;

    /// <summary>How often to poll for new alerts. Default 5 minutes.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Fired when alerts are refreshed. Args contains the current active alerts.</summary>
    public event EventHandler<IReadOnlyList<NwsAlertRecord>>? AlertsRefreshed;

    /// <summary>The most recently fetched alerts.</summary>
    public IReadOnlyList<NwsAlertRecord> CurrentAlerts { get; private set; } = [];

    public NwsAlertService()
    {
        http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.2.0 (github.com/KE4CON/APRS-Command)");
        http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>Starts the background poll loop.</summary>
    public void Start(double latitude, double longitude)
    {
        if (pollLoop is not null) return;

        pollLoop = Task.Run(async () =>
        {
            // Fetch immediately on start.
            await FetchAsync(latitude, longitude).ConfigureAwait(false);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PollInterval, cts.Token).ConfigureAwait(false);
                    await FetchAsync(latitude, longitude).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch { /* network error — try again next cycle */ }
            }
        }, cts.Token);
    }

    /// <summary>Fetches alerts immediately for the given location.</summary>
    public async Task FetchAsync(double latitude, double longitude)
    {
        try
        {
            var url = $"https://api.weather.gov/alerts/active?point={latitude:F4},{longitude:F4}";
            var response = await http.GetAsync(url, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cts.Token).ConfigureAwait(false);

            var alerts = new List<NwsAlertRecord>();
            if (json.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var props)) continue;

                    var alert = ParseAlert(props);
                    if (alert is not null && !alert.IsExpired)
                        alerts.Add(alert);
                }
            }

            // Sort by severity descending.
            alerts.Sort((a, b) => b.SeverityLevel.CompareTo(a.SeverityLevel));

            CurrentAlerts = alerts;
            AlertsRefreshed?.Invoke(this, alerts);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* swallow — network errors are expected in field conditions */ }
    }

    private static NwsAlertRecord? ParseAlert(JsonElement props)
    {
        try
        {
            var id          = props.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var evt         = props.TryGetProperty("event", out var evEl) ? evEl.GetString() ?? "" : "";
            var headline    = props.TryGetProperty("headline", out var hlEl) ? hlEl.GetString() ?? "" : "";
            var description = props.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "";
            var severity    = props.TryGetProperty("severity", out var sevEl) ? sevEl.GetString() ?? "Unknown" : "Unknown";
            var urgency     = props.TryGetProperty("urgency", out var urgEl) ? urgEl.GetString() ?? "" : "";
            var certainty   = props.TryGetProperty("certainty", out var certEl) ? certEl.GetString() ?? "" : "";
            var area        = props.TryGetProperty("areaDesc", out var areaEl) ? areaEl.GetString() ?? "" : "";
            var sender      = props.TryGetProperty("senderName", out var sndEl) ? sndEl.GetString() ?? "" : "";

            DateTimeOffset effective = DateTimeOffset.UtcNow;
            if (props.TryGetProperty("effective", out var effEl) && effEl.GetString() is { } effStr)
                DateTimeOffset.TryParse(effStr, out effective);

            DateTimeOffset? expires = null;
            if (props.TryGetProperty("expires", out var expEl) && expEl.GetString() is { } expStr
                && DateTimeOffset.TryParse(expStr, out var expVal))
                expires = expVal;

            return new NwsAlertRecord(id, evt, headline, description, severity, urgency,
                certainty, area, effective, expires, sender);
        }
        catch { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        if (pollLoop is not null)
        {
            try { await pollLoop.ConfigureAwait(false); } catch { }
        }
        http.Dispose();
        cts.Dispose();
    }
}
