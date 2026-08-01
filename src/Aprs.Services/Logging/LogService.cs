namespace Aprs.Services;

/// <summary>
/// Default <see cref="ILogService"/>: keeps a bounded in-memory ring of recent entries (for a UI log
/// view / export), raises <see cref="EntryLogged"/> for live updates, and mirrors each entry to the
/// debugger via <see cref="System.Diagnostics.Debug"/>. Thread-safe.
/// </summary>
public sealed class LogService : ILogService
{
    private readonly int capacity;
    private readonly object gate = new();
    private readonly Queue<LogEntry> entries = new();

    public LogService(int capacity = 500)
    {
        this.capacity = capacity < 1 ? 1 : capacity;
    }

    public event EventHandler<LogEntry>? EntryLogged;

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry(
            DateTimeOffset.UtcNow,
            level,
            string.IsNullOrWhiteSpace(category) ? "General" : category,
            message ?? string.Empty,
            exception?.ToString());

        lock (gate)
        {
            entries.Enqueue(entry);
            while (entries.Count > capacity)
            {
                entries.Dequeue();
            }
        }

        System.Diagnostics.Debug.WriteLine(
            $"[{entry.TimestampUtc:HH:mm:ss}Z] {entry.Level} {entry.Category}: {entry.Message}"
            + (entry.ExceptionDetail is null ? string.Empty : $" — {entry.ExceptionDetail}"));

        EntryLogged?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> GetRecent()
    {
        lock (gate)
        {
            return entries.ToArray();
        }
    }
}
