namespace Aprs.Desktop.Runtime;

/// <summary>A single position in a station's trail history.</summary>
public sealed record TrailPoint(double Latitude, double Longitude, DateTimeOffset ReceivedAt);

/// <summary>
/// Tracks the recent position history of APRS stations so their trail can be drawn on the map.
/// Keeps the last <see cref="MaxPointsPerStation"/> positions per callsign, and drops points
/// older than <see cref="MaxAge"/>.
/// </summary>
public sealed class StationTrailService
{
    private readonly Dictionary<string, List<TrailPoint>> trails = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum position history per station. Default 30 points.</summary>
    public int MaxPointsPerStation { get; init; } = 30;

    /// <summary>Maximum age of a trail point. Default 2 hours.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Fired when any station's trail changes.</summary>
    public event EventHandler? TrailsUpdated;

    /// <summary>Records a new position for the given callsign.</summary>
    public void RecordPosition(string callsign, double latitude, double longitude, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;

        if (!trails.TryGetValue(callsign, out var points))
        {
            points = [];
            trails[callsign] = points;
        }

        // Don't add duplicate positions (same lat/lon — station is stationary).
        if (points.Count > 0)
        {
            var last = points[^1];
            if (Math.Abs(last.Latitude - latitude) < 0.0001 && Math.Abs(last.Longitude - longitude) < 0.0001)
                return;
        }

        points.Add(new TrailPoint(latitude, longitude, receivedAt));

        // Keep within limits.
        var cutoff = DateTimeOffset.UtcNow - MaxAge;
        points.RemoveAll(p => p.ReceivedAt < cutoff);
        while (points.Count > MaxPointsPerStation)
            points.RemoveAt(0);

        TrailsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns trail points for all stations that have more than one position recorded.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TrailPoint>> GetTrails()
    {
        var result = new Dictionary<string, IReadOnlyList<TrailPoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (callsign, points) in trails)
        {
            if (points.Count >= 2)
                result[callsign] = points.AsReadOnly();
        }
        return result;
    }

    /// <summary>Removes all trail data for all stations.</summary>
    public void Clear()
    {
        trails.Clear();
        TrailsUpdated?.Invoke(this, EventArgs.Empty);
    }
}
