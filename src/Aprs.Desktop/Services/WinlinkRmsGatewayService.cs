using System.Net.Http;
using System.Text.Json;
using Aprs.Mapping;

namespace Aprs.Desktop.Services;

/// <summary>
/// Fetches nearby Winlink RMS gateway stations for display on the map.
///
/// PENDING: requires a Winlink API access key, issued per application/author by a Winlink
/// administrator at https://api.winlink.org (not self-service). See WinlinkSettings for where the
/// key is configured once obtained.
///
/// Request/response shapes are modeled on the published Winlink CMS Web Services DTOs
/// (github.com/ARSFI/winlink-webservices — <c>GatewayProximity</c> / <c>GatewayPositionsRecord</c>):
/// the <c>/gateway/proximity</c> endpoint returns gateways near a <b>grid square</b>, sorted by
/// distance. The operator's lat/lon is converted to a grid square for the request, and each
/// gateway's returned grid square is converted back to lat/lon for the map.
///
/// Two details remain to confirm against a live key (documented but untested): the integer
/// <c>Mode</c> enum mapping, and that ServiceStack returns PascalCase JSON as assumed here.
/// </summary>
public sealed class WinlinkRmsGatewayService : IDisposable
{
    // Gateway proximity endpoint (Route "/gateway/proximity" in the published DTOs).
    private const string ProximityUrl = "https://api.winlink.org/gateway/proximity";
    private const double KmPerMile = 1.609344;

    private readonly HttpClient http;
    private readonly string? apiKey;

    public WinlinkRmsGatewayService(string? apiKey)
    {
        this.apiKey = apiKey;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(AppVersion.UserAgent);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey);

    public void Dispose() => http.Dispose();

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
            // The proximity endpoint takes a grid square + a distance in MILES (not lat/lon/km).
            var gridSquare = MaidenheadGridLocator.FromCoordinates(latitude, longitude);
            var maxDistanceMiles = (int)Math.Round(radiusKm / KmPerMile);

            // Parameters per the GatewayProximity DTO. Key is the required "Key" member.
            var url = $"{ProximityUrl}?format=json" +
                      $"&Key={Uri.EscapeDataString(apiKey!)}" +
                      $"&GridSquare={Uri.EscapeDataString(gridSquare)}" +
                      $"&ServiceCodes=PUBLIC" +
                      $"&HistoryHours=48" +
                      $"&MaxDistance={maxDistanceMiles}" +
                      $"&OperatingMode=AnyAll";

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
    /// Parses a <c>GatewayProximityResponse</c>: <c>{ "GatewayList": [ { "Callsign", "Gridsquare",
    /// "Frequency"(int Hz), "Mode"(int), "Baud", "ServiceCode", "Distance", "Heading" } ] }</c>.
    /// The gateway location is a grid square, converted here to lat/lon for the map.
    /// Public for unit testing against a documented-shape sample response.
    /// </summary>
    public static List<WinlinkGateway> ParseResponse(string json)
    {
        var gateways = new List<WinlinkGateway>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("GatewayList", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return gateways;
            }

            foreach (var item in list.EnumerateArray())
            {
                var callsign = GetString(item, "Callsign");
                if (string.IsNullOrWhiteSpace(callsign))
                {
                    continue;
                }

                var (latitude, longitude) = GridToCoordinates(GetString(item, "Gridsquare"));

                var freqHz = GetInt(item, "Frequency");
                var frequency = freqHz > 0 ? $"{freqHz / 1_000_000.0:0.000###} MHz" : string.Empty;
                var mode = FormatMode(GetInt(item, "Mode"), GetString(item, "Baud"));

                gateways.Add(new WinlinkGateway(callsign, latitude, longitude, frequency, mode));
            }
        }
        catch { /* malformed response — return what we have */ }

        return gateways;
    }

    private static (double Latitude, double Longitude) GridToCoordinates(string grid)
    {
        if (!string.IsNullOrWhiteSpace(grid) && grid.Length >= 4)
        {
            try { return MaidenheadGridLocator.ToCoordinates(grid); }
            catch { /* malformed grid — fall through to 0,0 */ }
        }

        return (0, 0);
    }

    // The record's integer Mode maps to Winlink's operating modes. This ordering follows the
    // published GatewayOperatingMode enum and is provisional until confirmed against a live response.
    private static string FormatMode(int mode, string baud)
    {
        var name = mode switch
        {
            1 => "Packet",
            2 => "Pactor",
            3 => "Winmor",
            4 => "Robust Packet",
            5 => "HF",
            6 => "ARDOP",
            7 => "VARA",
            8 => "VARA FM",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(baud))
        {
            return string.IsNullOrEmpty(name) ? $"{baud} baud" : $"{name} ({baud})";
        }

        return name;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
            ? i
            : 0;
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
