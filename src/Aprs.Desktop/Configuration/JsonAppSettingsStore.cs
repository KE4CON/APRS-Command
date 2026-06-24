using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aprs.Services;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// JSON-file implementation of <see cref="IAppSettingsStore"/>. Stores the whole
/// <see cref="AppSettings"/> tree as a single human-readable <c>settings.json</c> under the
/// application's config folder.
///
/// <para>Design points:</para>
/// <list type="bullet">
/// <item><b>One source of truth.</b> All persisted settings live in one file under the app's
/// config folder (reconciling the older ad-hoc per-user path that the station profile used).</item>
/// <item><b>Never throws on load.</b> Missing file → defaults. Whole-file parse error → a salvage
/// pass deserializes each section independently so one bad section cannot lose the others.</item>
/// <item><b>Atomic save.</b> Writes a temp file then moves it into place, so an interrupted write
/// cannot leave a half-written, unparseable file.</item>
/// <item><b>Versioned + migratable.</b> Every save stamps <see cref="AppSettings.CurrentSchemaVersion"/>;
/// older versions are migrated forward on load via <see cref="Migrate"/>.</item>
/// <item><b>Legacy import.</b> On first load, an existing legacy <c>station-profile.json</c> is
/// imported so operators do not lose a profile saved by an earlier build.</item>
/// </list>
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string settingsFilePath;
    private readonly string? legacyStationProfilePath;

    /// <param name="settingsFilePath">Full path to the settings JSON file.</param>
    /// <param name="legacyStationProfilePath">
    /// Optional path to an older station-profile.json to import once if no settings file exists yet.
    /// </param>
    public JsonAppSettingsStore(string settingsFilePath, string? legacyStationProfilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        this.settingsFilePath = settingsFilePath;
        this.legacyStationProfilePath = legacyStationProfilePath;
    }

    /// <summary>
    /// The process-wide default store, pointed at <c>&lt;app-data&gt;/config/settings.json</c> and
    /// aware of the legacy station-profile location for one-time import. This is what
    /// <see cref="StationProfile.Load"/> / <see cref="StationProfile.Save"/> route through.
    /// </summary>
    public static JsonAppSettingsStore Default { get; } = CreateDefault();

    private static JsonAppSettingsStore CreateDefault()
    {
        var layout = ApplicationFolderLayout.FromRoot(ApplicationFolderLayout.GetDefaultApplicationDataFolder());
        var settingsPath = Path.Combine(layout.ConfigFolderPath, "settings.json");

        // Where earlier builds saved the minimal station profile.
        var legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AprsCommand",
            "station-profile.json");

        return new JsonAppSettingsStore(settingsPath, legacyPath);
    }

    public AppSettings Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            var migrated = TryImportLegacy();
            if (migrated is not null)
            {
                Save(migrated);          // persist the imported profile in the new location
                return migrated;
            }

            return AppSettings.Default;
        }

        string json;
        try
        {
            json = File.ReadAllText(settingsFilePath);
        }
        catch
        {
            return AppSettings.Default;  // unreadable file: start from defaults, never crash
        }

        // Fast path: deserialize the whole file at once.
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is not null)
            {
                return Normalize(settings);
            }
        }
        catch
        {
            // fall through to per-section salvage
        }

        return SalvageLoad(json);
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Always persist with the current schema version stamped.
        var toWrite = settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };

        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(toWrite, JsonOptions);

        // Atomic write: temp file then move-with-overwrite so a partial write cannot corrupt the file.
        var tempPath = settingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, settingsFilePath, overwrite: true);
    }

    public AppSettings Update(Func<AppSettings, AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        var updated = mutate(Load());
        Save(updated);
        return updated;
    }

    /// <summary>Fixes up a loaded tree: fills missing sections, validates, and migrates old versions.</summary>
    private static AppSettings Normalize(AppSettings settings)
    {
        var station = settings.Station;
        if (station is null || string.IsNullOrWhiteSpace(station.Callsign))
        {
            station = StationProfile.Default;
        }

        var connections = settings.Connections ?? ConnectionSettings.Default;

        var normalized = settings with { Station = station, Connections = connections };
        return Migrate(normalized);
    }

    /// <summary>
    /// Forward-migrates an older schema. No transformations exist yet (v1 is current); this is the
    /// hook where future format changes are applied step by step so saved data is never dropped.
    /// </summary>
    private static AppSettings Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion >= AppSettings.CurrentSchemaVersion)
        {
            return settings;
        }

        // Example for the future:
        //   if (settings.SchemaVersion < 2) { settings = settings with { ... }; }

        return settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
    }

    /// <summary>
    /// Best-effort load when the whole-file parse failed: parse the JSON object and deserialize
    /// each section on its own, falling back to that section's default if it is malformed. This
    /// keeps a good connections section even if, say, the station section was hand-edited badly.
    /// </summary>
    private static AppSettings SalvageLoad(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return AppSettings.Default;
        }

        if (root is null)
        {
            return AppSettings.Default;
        }

        var station = TryDeserializeSection(root, "station", StationProfile.Default);
        if (station is null || string.IsNullOrWhiteSpace(station.Callsign))
        {
            station = StationProfile.Default;
        }

        var connections = TryDeserializeSection(root, "connections", ConnectionSettings.Default)
            ?? ConnectionSettings.Default;

        var schemaVersion = AppSettings.CurrentSchemaVersion;
        if (root.TryGetPropertyValue("schemaVersion", out var versionNode)
            && versionNode is not null
            && int.TryParse(versionNode.ToString(), out var parsedVersion))
        {
            schemaVersion = parsedVersion;
        }

        return Migrate(new AppSettings(schemaVersion, station, connections));
    }

    private static T? TryDeserializeSection<T>(JsonObject root, string name, T fallback)
        where T : class
    {
        try
        {
            // Property names were written camelCase; reads are case-insensitive anyway.
            foreach (var kvp in root)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value?.Deserialize<T>(JsonOptions) ?? fallback;
                }
            }
        }
        catch
        {
            // malformed section: use the fallback
        }

        return fallback;
    }

    /// <summary>Imports a legacy minimal station-profile.json into a fresh settings tree, if present and valid.</summary>
    private AppSettings? TryImportLegacy()
    {
        if (string.IsNullOrWhiteSpace(legacyStationProfilePath) || !File.Exists(legacyStationProfilePath))
        {
            return null;
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<StationProfile>(
                File.ReadAllText(legacyStationProfilePath),
                JsonOptions);

            if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.Callsign))
            {
                return AppSettings.Default with { Station = legacy };
            }
        }
        catch
        {
            // ignore a bad legacy file; caller falls back to defaults
        }

        return null;
    }
}
