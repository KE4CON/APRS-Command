using System.Net.Http;
using System.Text.Json;

namespace Aprs.Desktop.Services;

/// <summary>
/// Fetches terrain elevation data along a great circle path between two points.
/// Uses the Open Elevation API (api.open-elevation.com) — free, no API key required.
///
/// Samples 20 evenly-spaced points along the path and returns elevation in metres
/// for each point. Used to generate an elevation profile for line-of-sight analysis.
/// </summary>
public sealed class ElevationProfileService : IAsyncDisposable
{
    private const string ApiUrl = "https://api.open-elevation.com/api/v1/lookup";
    private const int SampleCount = 20;

    private readonly HttpClient http;

    public ElevationProfileService()
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.3.0 (github.com/KE4CON/APRS-Command)");
    }

    /// <summary>
    /// Fetches elevation profile between two geographic points.
    /// Returns null if the API is unreachable.
    /// </summary>
    public async Task<ElevationProfile?> GetProfileAsync(
        double fromLat, double fromLon,
        double toLat, double toLon,
        CancellationToken cancellationToken = default)
    {
        // Generate intermediate points along the great circle path.
        var points = InterpolatePoints(fromLat, fromLon, toLat, toLon, SampleCount);

        // Build comma-separated locations query.
        var locations = string.Join("|",
            points.Select(p => $"{p.Lat:F6},{p.Lon:F6}"));

        var url      = $"{ApiUrl}?locations={Uri.EscapeDataString(locations)}";
        var response = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var doc      = JsonDocument.Parse(response);

        if (!doc.RootElement.TryGetProperty("results", out var results))
            return null;

        var elevations = results.EnumerateArray()
            .Select(r => r.TryGetProperty("elevation", out var el) ? el.GetDouble() : 0.0)
            .ToList();

        if (elevations.Count != points.Count) return null;

        var profilePoints = points.Zip(elevations, (p, e) => new ElevationPoint(
            p.Lat, p.Lon, e, p.DistanceKm)).ToList();

        return new ElevationProfile(
            FromLat: fromLat, FromLon: fromLon,
            ToLat: toLat, ToLon: toLon,
            TotalDistanceKm: points[^1].DistanceKm,
            Points: profilePoints,
            MinElevationM: profilePoints.Min(p => p.ElevationM),
            MaxElevationM: profilePoints.Max(p => p.ElevationM));
    }

    /// <summary>
    /// Interpolates N evenly spaced points along the great circle path.
    /// Uses simple linear interpolation (accurate enough for amateur radio distances).
    /// </summary>
    private static List<(double Lat, double Lon, double DistanceKm)> InterpolatePoints(
        double fromLat, double fromLon, double toLat, double toLon, int count)
    {
        var points = new List<(double, double, double)>(count);
        var totalKm = HaversineKm(fromLat, fromLon, toLat, toLon);

        for (int i = 0; i < count; i++)
        {
            var t   = (double)i / (count - 1);
            var lat = fromLat + t * (toLat - fromLat);
            var lon = fromLon + t * (toLon - fromLon);
            var km  = totalKm * t;
            points.Add((lat, lon, km));
        }
        return points;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        await ValueTask.CompletedTask;
    }
}

public sealed record ElevationPoint(
    double Lat, double Lon,
    double ElevationM,
    double DistanceKm);

public sealed record ElevationProfile(
    double FromLat, double FromLon,
    double ToLat, double ToLon,
    double TotalDistanceKm,
    IReadOnlyList<ElevationPoint> Points,
    double MinElevationM,
    double MaxElevationM)
{
    /// <summary>Total elevation change from start to end (positive = uphill).</summary>
    public double ElevationChangeM => Points[^1].ElevationM - Points[0].ElevationM;

    /// <summary>Renders a simple ASCII elevation profile chart.</summary>
    public string ToAsciiChart(int width = 60, int height = 12)
    {
        var sb = new System.Text.StringBuilder();
        var range  = MaxElevationM - MinElevationM;
        var scale  = range > 0 ? height / range : 1;

        sb.AppendLine($"Elevation Profile — {TotalDistanceKm:F1} km");
        sb.AppendLine($"Min: {MinElevationM:F0} m   Max: {MaxElevationM:F0} m   Change: {ElevationChangeM:+0.0;-0.0} m");
        sb.AppendLine();

        // Build chart grid
        var chart = new char[height, width];
        for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
                chart[r, c] = ' ';

        for (int c = 0; c < width; c++)
        {
            var pointIdx = (int)((double)c / (width - 1) * (Points.Count - 1));
            var elev = Points[pointIdx].ElevationM;
            var row  = (int)((elev - MinElevationM) * scale);
            row = Math.Clamp(row, 0, height - 1);
            // Fill from ground to elevation
            for (int r = 0; r <= row; r++)
                chart[height - 1 - r, c] = r == row ? '▲' : '█';
        }

        // Print chart with Y-axis labels
        for (int r = 0; r < height; r++)
        {
            var elev = MinElevationM + (height - 1 - r) / scale;
            if (r == 0 || r == height - 1 || r == height / 2)
                sb.Append($"{elev,6:F0}m│");
            else
                sb.Append($"       │");

            for (int c = 0; c < width; c++)
                sb.Append(chart[r, c]);
            sb.AppendLine();
        }

        sb.AppendLine($"       └{'─'.ToString().PadRight(width, '─')}");
        sb.AppendLine($"       0{string.Empty.PadRight(width / 2 - 3)}{(TotalDistanceKm / 2):F1} km{string.Empty.PadRight(width / 2 - 5)}{TotalDistanceKm:F1} km");

        return sb.ToString();
    }
}
