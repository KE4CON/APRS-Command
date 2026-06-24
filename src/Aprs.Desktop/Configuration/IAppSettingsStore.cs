namespace Aprs.Desktop.Configuration;

/// <summary>
/// Loads and saves the application's persisted settings. Implementations must never throw on
/// load: a missing or corrupt file falls back to defaults so the app always starts. This is the
/// single source of truth for persisted configuration — UI editors read and write through it.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Returns the persisted settings, or sensible defaults if nothing is saved yet or the file
    /// cannot be read. A corrupt file is salvaged section by section where possible rather than
    /// discarded wholesale.
    /// </summary>
    AppSettings Load();

    /// <summary>Writes the given settings atomically (temp file then move) so a crash mid-write cannot corrupt the file.</summary>
    void Save(AppSettings settings);

    /// <summary>
    /// Loads the current settings, applies <paramref name="mutate"/>, saves the result, and
    /// returns it. Use this to change one section without clobbering the others.
    /// </summary>
    AppSettings Update(Func<AppSettings, AppSettings> mutate);
}
