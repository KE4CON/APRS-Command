namespace Aprs.Services;

/// <summary>
/// Manages a list of time-of-day beacon triggers. Call <see cref="TickAsync"/> from
/// the application heartbeat (e.g. every 30 seconds) to evaluate due triggers and
/// fire beacons through the provided beacon delegate.
/// </summary>
public sealed class ScheduledBeaconService
{
    private readonly List<ScheduledBeaconEntry>  entries     = [];
    private readonly Dictionary<Guid, DateOnly>  lastFired   = [];
    private readonly Func<string?, Task>          beaconAsync;

    /// <param name="beaconAsync">
    /// Delegate that fires a beacon. The string argument is an optional comment override;
    /// null means use the station's configured comment.
    /// </param>
    public ScheduledBeaconService(Func<string?, Task> beaconAsync)
    {
        this.beaconAsync = beaconAsync;
    }

    // ── Entry management ──────────────────────────────────────────────────────

    public IReadOnlyList<ScheduledBeaconEntry> Entries => entries;

    public void Add(ScheduledBeaconEntry entry)
    {
        entries.RemoveAll(e => e.Id == entry.Id);
        entries.Add(entry);
    }

    public void Remove(Guid id) => entries.RemoveAll(e => e.Id == id);

    public void Replace(ScheduledBeaconEntry updated) => Add(updated);

    public void Clear() => entries.Clear();

    // ── Tick ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates all scheduled entries against the current time. Fires beacons for
    /// any entry that is enabled, applies to today, has not already fired today,
    /// and whose scheduled time has passed.
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        var now   = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.DateTime);
        var time  = TimeOnly.FromDateTime(now.DateTime);

        foreach (var entry in entries.Where(e => e.Enabled))
        {
            if (!entry.AppliesToDay(now.DayOfWeek)) continue;
            if (lastFired.TryGetValue(entry.Id, out var fired) && fired == today) continue;
            if (time < entry.FireAt) continue;

            lastFired[entry.Id] = today;
            try
            {
                await beaconAsync(entry.CustomComment).ConfigureAwait(false);
            }
            catch
            {
                // Beacon failure is non-fatal — log would be ideal but we keep this simple.
            }
        }
    }

    /// <summary>Returns a human-readable status string for display.</summary>
    public string StatusSummary =>
        entries.Count == 0
            ? "No scheduled beacons configured."
            : $"{entries.Count(e => e.Enabled)} of {entries.Count} scheduled beacon{(entries.Count == 1 ? "" : "s")} enabled.";
}
