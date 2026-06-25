namespace Aprs.Desktop.Configuration;

/// <summary>
/// An <see cref="IAppSettingsStore"/> that keeps settings in memory only. Used for design-time view
/// models and for unit tests that exercise settings-backed view models without touching disk.
/// </summary>
public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    private AppSettings settings;

    public InMemoryAppSettingsStore(AppSettings? initial = null)
        => settings = (initial ?? AppSettings.Default) with { Connections = (initial ?? AppSettings.Default).Connections.Normalized() };

    public AppSettings Load() => settings;

    public void Save(AppSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        settings = value with { SchemaVersion = AppSettings.CurrentSchemaVersion };
    }

    public AppSettings Update(Func<AppSettings, AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        Save(mutate(Load()));
        return Load();
    }
}
