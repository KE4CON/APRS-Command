namespace Aprs.Services;

/// <summary>Severity of a diagnostic log entry.</summary>
public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

/// <summary>A single diagnostic log entry. Immutable; safe to hand to a UI log view.</summary>
public sealed record LogEntry(
    DateTimeOffset TimestampUtc,
    LogLevel Level,
    string Category,
    string Message,
    string? ExceptionDetail);

/// <summary>
/// Minimal diagnostic-logging abstraction. Replaces scattered <c>Debug.WriteLine</c> calls so
/// unexpected faults (a faulted watchdog loop, a failed failover switch) are recorded somewhere a
/// UI log view or export can surface them — not lost to a debugger-only channel. Implementations
/// must be safe to call from any thread.
/// </summary>
public interface ILogService
{
    /// <summary>Records a log entry.</summary>
    void Log(LogLevel level, string category, string message, Exception? exception = null);

    /// <summary>A snapshot of the most recent entries, oldest first.</summary>
    IReadOnlyList<LogEntry> GetRecent();

    /// <summary>Raised for each entry recorded, so a UI log view can update live.</summary>
    event EventHandler<LogEntry>? EntryLogged;

    void Information(string category, string message) => Log(LogLevel.Information, category, message);
    void Warning(string category, string message, Exception? exception = null) => Log(LogLevel.Warning, category, message, exception);
    void Error(string category, string message, Exception? exception = null) => Log(LogLevel.Error, category, message, exception);
}
