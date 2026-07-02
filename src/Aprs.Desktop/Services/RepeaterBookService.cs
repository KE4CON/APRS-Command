using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aprs.Desktop.Services;

/// <summary>
/// Client for the RepeaterBook Export API.
/// Requires an approved API token from repeaterbook.com/api/token_request.php
///
/// API documentation: https://www.repeaterbook.com/wiki/doku.php?id=api
/// Authentication: X-RB-App-Token header (preferred) or Bearer token.
/// Rate limiting: back off immediately on 429 responses.
/// All queries are strictly user-initiated — no background polling.
/// </summary>
public sealed class RepeaterBookService : IDisposable
{
    private const string BaseUrl    = "https://www.repeaterbook.com/api/export.php";
    private const string UserAgent  = "APRS-Command/1.0 (github.com/KE4CON/APRS-Command; KE4CON)";
    private const int    MinQueryIntervalSeconds = 10;

    private readonly HttpClient http;
    private DateTimeOffset lastQueryAt = DateTimeOffset.MinValue;

    public RepeaterBookService()
    {
        http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Search for repeaters near a location.
    /// </summary>
    /// <param name="token">Approved RepeaterBook API token.</param>
    /// <param name="latitude">Station latitude in decimal degrees.</param>
    /// <param name="longitude">Station longitude in decimal degrees.</param>
    /// <param name="radiusMiles">Search radius in miles (1-250).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<RepeaterBookResult> SearchProximityAsync(
        string token,
        double latitude,
        double longitude,
        int radiusMiles,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return RepeaterBookResult.NoToken();

        // Enforce minimum interval between queries
        var elapsed = DateTimeOffset.UtcNow - lastQueryAt;
        if (elapsed.TotalSeconds < MinQueryIntervalSeconds)
            return RepeaterBookResult.RateLimited(
                $"Please wait {(int)(MinQueryIntervalSeconds - elapsed.TotalSeconds)} more seconds before refreshing.");

        var url = $"{BaseUrl}?country_id=1&proximity={radiusMiles}&lat={latitude:F6}&lng={longitude:F6}&format=json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-RB-App-Token", token);

        try
        {
            lastQueryAt = DateTimeOffset.UtcNow;
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                return RepeaterBookResult.RateLimited("RepeaterBook rate limit reached. Please wait a few minutes before trying again.");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
             || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return RepeaterBookResult.AuthError("API token rejected. Check your token in Settings → Connections → RepeaterBook API Token.");

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<RepeaterBookResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null || parsed.Results is null)
                return RepeaterBookResult.Empty();

            if (parsed.Ok == false)
                return RepeaterBookResult.ApiError(parsed.Message ?? "Unknown API error.");

            var repeaters = parsed.Results
                .Select(r => new RepeaterEntry(
                    Callsign:        r.Callsign ?? string.Empty,
                    Frequency:       r.Frequency ?? string.Empty,
                    InputFrequency:  r.InputFreq ?? string.Empty,
                    OffsetMhz:       ParseDouble(r.Offset),
                    CtcssTone:       r.Tone ?? string.Empty,
                    DcsCode:         r.Dcs ?? string.Empty,
                    City:            r.City ?? string.Empty,
                    State:           r.State ?? string.Empty,
                    DistanceMiles:   ParseDouble(r.Distance),
                    Operational:     string.Equals(r.Operational, "Yes", StringComparison.OrdinalIgnoreCase),
                    Notes:           r.Notes ?? string.Empty,
                    Use:             r.Use ?? string.Empty,
                    LastUpdate:      r.Lastupdated ?? string.Empty))
                .OrderBy(r => r.DistanceMiles)
                .ToList();

            return RepeaterBookResult.Success(repeaters);
        }
        catch (OperationCanceledException)
        {
            return RepeaterBookResult.ApiError("Request cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return RepeaterBookResult.ApiError($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return RepeaterBookResult.ApiError($"Unexpected error: {ex.Message}");
        }
    }

    private static double ParseDouble(string? s)
        => double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    public void Dispose() => http.Dispose();

    // ── JSON models ────────────────────────────────────────────────────────────
    private sealed class RepeaterBookResponse
    {
        [JsonPropertyName("ok")]    public bool?   Ok      { get; set; }
        [JsonPropertyName("count")] public int?    Count   { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("results")] public List<RbRepeater>? Results { get; set; }
    }

    private sealed class RbRepeater
    {
        [JsonPropertyName("Callsign")]    public string? Callsign    { get; set; }
        [JsonPropertyName("Frequency")]   public string? Frequency   { get; set; }
        [JsonPropertyName("Input Freq")]  public string? InputFreq   { get; set; }
        [JsonPropertyName("Offset")]      public string? Offset      { get; set; }
        [JsonPropertyName("Tone")]        public string? Tone        { get; set; }
        [JsonPropertyName("DCS")]         public string? Dcs         { get; set; }
        [JsonPropertyName("City")]        public string? City        { get; set; }
        [JsonPropertyName("State")]       public string? State       { get; set; }
        [JsonPropertyName("Distance")]    public string? Distance    { get; set; }
        [JsonPropertyName("Operational")] public string? Operational { get; set; }
        [JsonPropertyName("Notes")]       public string? Notes       { get; set; }
        [JsonPropertyName("Use")]         public string? Use         { get; set; }
        [JsonPropertyName("Last Updated")] public string? Lastupdated { get; set; }
    }
}

// ── Result types ───────────────────────────────────────────────────────────────

public enum RepeaterBookStatus
{
    Success, NoToken, RateLimited, AuthError, ApiError, Empty
}

public sealed class RepeaterBookResult
{
    public RepeaterBookStatus        Status    { get; private init; }
    public string                    Message   { get; private init; } = string.Empty;
    public IReadOnlyList<RepeaterEntry> Repeaters { get; private init; } = [];

    public static RepeaterBookResult Success(IReadOnlyList<RepeaterEntry> r)
        => new() { Status = RepeaterBookStatus.Success, Repeaters = r };
    public static RepeaterBookResult NoToken()
        => new() { Status = RepeaterBookStatus.NoToken,
                   Message = "No RepeaterBook API token configured. Enter your token in Settings → Connections → RepeaterBook API Token." };
    public static RepeaterBookResult RateLimited(string msg)
        => new() { Status = RepeaterBookStatus.RateLimited, Message = msg };
    public static RepeaterBookResult AuthError(string msg)
        => new() { Status = RepeaterBookStatus.AuthError, Message = msg };
    public static RepeaterBookResult ApiError(string msg)
        => new() { Status = RepeaterBookStatus.ApiError, Message = msg };
    public static RepeaterBookResult Empty()
        => new() { Status = RepeaterBookStatus.Empty,
                   Message = "No repeaters found in this area. Try increasing the search radius." };
}

public sealed record RepeaterEntry(
    string Callsign,
    string Frequency,
    string InputFrequency,
    double OffsetMhz,
    string CtcssTone,
    string DcsCode,
    string City,
    string State,
    double DistanceMiles,
    bool   Operational,
    string Notes,
    string Use,
    string LastUpdate)
{
    public string OffsetLabel => OffsetMhz switch
    {
        > 0  => $"+{OffsetMhz:F3}",
        < 0  => $"{OffsetMhz:F3}",
        _    => "0"
    };

    public string ToneLabel => !string.IsNullOrEmpty(DcsCode) && DcsCode != "0"
        ? $"DCS {DcsCode}"
        : !string.IsNullOrEmpty(CtcssTone) && CtcssTone != "0"
            ? $"{CtcssTone} Hz"
            : "None";

    public string DistanceLabel => $"{DistanceMiles:F1} mi";
    public string LocationLabel => string.IsNullOrEmpty(State) ? City : $"{City}, {State}";
}
