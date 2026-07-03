using System.Net.Http;
using System.Text.Json;

namespace Aprs.Desktop.Services;

/// <summary>
/// Fetches nearby Winlink RMS gateway stations for display on the map.
///
/// PENDING: This service requires a Winlink API access key, which must be
/// requested directly from a Winlink administrator at https://api.winlink.org —
/// it is not a free, self-service API. The key is issued per application
/// and software author. See WinlinkSettings for where the key is configured
/// once obtained.
///
/// The HTTP call below targets the documented Winlink gateway/query endpoint
/// per https://api.winlink.org but has not been tested against a live key.
/// Verify the request/response shape once a key is available and adjust
/// ParseResponse() accordingly.
/// </summary>
public sealed class WinlinkRmsGatewayService
{
    private const string ApiBaseUrl = "https://api.winlink.org/gateway/query";
    private readonly HttpClient http;
    private readonly string? apiKey;

    public WinlinkRmsGatewayService(string? apiKey)
    {
        this.apiKey = apiKey;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(AppVersion.UserAgent);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey);

    /// <summary>
    /// Fetches RMS gateways within range of the given position.
    /// Returns an empty list and a guidance message if no API key is configured.
    /// </summary>
    public async Task<WinlinkGatewayQueryResult> QueryNearbyGatewaysAsync(
        double latitude, double longitude, double radiusKm = 200,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return WinlinkGatewayQueryResult.NeedsApiKey(
                "Winlink API key not configured. Request a key from a Winlink " +
                "administrator at https://api.winlink.org, then enter it in " +
                "Settings → Winlink.");
        }

        try
        {
            // NOTE: exact query parameters per the api.winlink.org documentation —
            // adjust once verified against a live key.
            var url = $"{ApiBaseUrl}?key={Uri.EscapeDataString(apiKey!)}" +
                      $"&lat={latitude:F6}&lon={longitude:F6}&radius={radiusKm:F0}" +
                      $"&format=json";

            var response = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var gateways = ParseResponse(response);

            return WinlinkGatewayQueryResult.Ok(gateways);
        }
        catch (Exception ex)
        {
            return WinlinkGatewayQueryResult.Failed($"Winlink API request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the Winlink API response JSON.
    /// PENDING VERIFICATION: this assumes a JSON array of gateway objects with
    /// callsign, latitude, longitude, frequency, mode, and lastReportUtc fields.
    /// Adjust field names once tested against the real API response.
    /// </summary>
    private static List<WinlinkGateway> ParseResponse(string json)
    {
        var gateways = new List<WinlinkGateway>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return gateways;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var callsign  = item.TryGetProperty("callsign", out var c) ? c.GetString() ?? "" : "";
                var lat       = item.TryGetProperty("latitude", out var la) ? la.GetDouble() : 0;
                var lon       = item.TryGetProperty("longitude", out var lo) ? lo.GetDouble() : 0;
                var frequency = item.TryGetProperty("frequency", out var f) ? f.GetString() ?? "" : "";
                var mode      = item.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";

                if (!string.IsNullOrWhiteSpace(callsign))
                    gateways.Add(new WinlinkGateway(callsign, lat, lon, frequency, mode));
            }
        }
        catch { /* malformed response — return what we have */ }

        return gateways;
    }
}

public sealed record WinlinkGateway(
    string Callsign,
    double Latitude,
    double Longitude,
    string Frequency,
    string Mode);

public sealed record WinlinkGatewayQueryResult(
    bool Success,
    bool NeedsKey,
    IReadOnlyList<WinlinkGateway> Gateways,
    string? Message)
{
    public static WinlinkGatewayQueryResult Ok(IReadOnlyList<WinlinkGateway> gateways) =>
        new(true, false, gateways, null);

    public static WinlinkGatewayQueryResult Failed(string message) =>
        new(false, false, [], message);

    public static WinlinkGatewayQueryResult NeedsApiKey(string message) =>
        new(false, true, [], message);
}
