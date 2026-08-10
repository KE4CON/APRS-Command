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

    // RecordPosition runs on the transport/ingest threads while GetTrails is read from the UI thread
    // to draw the map; without this lock the dictionary and its lists are mutated and enumerated
    // concurrently (audit Concurrency H2). Never held across a callback: TrailsUpdated is raised after
    // the lock is released, and GetTrails hands back deep copies so nothing mutable escapes the lock.
    private readonly object sync = new();

    /// <summary>Maximum position history per station. Default 30 points.</summary>
    public int MaxPointsPerStation { get; init; } = 30;

    /// <summary>
    /// Maximum number of distinct stations retained. A busy APRS-IS feed sees thousands of unique
    /// callsigns over a session; without a cap the trail dictionary grows unbounded. When exceeded,
    /// the station whose newest point is oldest is evicted (least-recently-heard). Default 500.
    /// </summary>
    public int MaxStations { get; init; } = 500;

    /// <summary>Maximum age of a trail point. Default 2 hours.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromHours(2);

    /// <summary>Fired when any station's trail changes.</summary>
    public event EventHandler? TrailsUpdated;

    /// <summary>Records a new position for the given callsign.</summary>
    public void RecordPosition(string callsign, double latitude, double longitude, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;

        lock (sync)
        {
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

            EvictOldestStationsIfNeeded();
        }

        TrailsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Returns deep copies of the trail points for all stations with more than one position.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TrailPoint>> GetTrails()
    {
        var result = new Dictionary<string, IReadOnlyList<TrailPoint>>(StringComparer.OrdinalIgnoreCase);
        lock (sync)
        {
            foreach (var (callsign, points) in trails)
            {
                if (points.Count >= 2)
                    result[callsign] = points.ToArray(); // copy — the live list keeps mutating under the lock
            }
        }
        return result;
    }

    /// <summary>Removes all trail data for all stations.</summary>
    public void Clear()
    {
        lock (sync)
        {
            trails.Clear();
        }
        TrailsUpdated?.Invoke(this, EventArgs.Empty);
    }

    // Caller must hold `sync`.
    private void EvictOldestStationsIfNeeded()
    {
        while (trails.Count > MaxStations)
        {
            string? oldestKey = null;
            var oldestNewest = DateTimeOffset.MaxValue;
            foreach (var (callsign, points) in trails)
            {
                // A station's recency is the timestamp of its most recent point.
                var newest = points.Count > 0 ? points[^1].ReceivedAt : DateTimeOffset.MinValue;
                if (newest < oldestNewest)
                {
                    oldestNewest = newest;
                    oldestKey = callsign;
                }
            }

            if (oldestKey is null) break;
            trails.Remove(oldestKey);
        }
    }
}
