namespace Aprs.Desktop.Configuration;

/// <summary>Persisted position and size for one window.</summary>
public sealed record WindowStateRecord(
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>
/// Persisted window positions and sizes, keyed by window type name.
/// </summary>
public sealed record WindowStates(
    IReadOnlyDictionary<string, WindowStateRecord> States)
{
    public static WindowStates Default { get; } = new(
        new Dictionary<string, WindowStateRecord>());

    public WindowStateRecord? Get(string key)
        => States.TryGetValue(key, out var s) ? s : null;

    public WindowStates With(string key, WindowStateRecord record)
    {
        var dict = new Dictionary<string, WindowStateRecord>(States)
        {
            [key] = record
        };
        return new WindowStates(dict);
    }
}
