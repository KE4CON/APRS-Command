namespace Aprs.Desktop.Configuration;

/// <summary>
/// The single root object for everything APRS Command persists between runs. Each new area of
/// configuration becomes a new section property here; the store loads and saves the whole tree
/// as one JSON file. Adding a section is the only change needed to make a new settings group
/// persist — no change to the store itself.
///
/// <para><see cref="SchemaVersion"/> exists so future format changes can be migrated rather than
/// silently dropped. The store stamps the current version on every save and can run migration
/// steps on load when an older version is read.</para>
/// </summary>
public sealed record AppSettings(
    int SchemaVersion,
    StationProfile Station,
    ConnectionSettings Connections,
    IGateSettings IGate,
    DigipeaterSettings Digipeater,
    AudioSettings Audio,
    WindowStates Windows,
    GpsSettings Gps,
    ManagedModemSettings ManagedModem,
    bool DarkMode,
    MessageTemplatesSettings MessageTemplates,
    SmartBeaconingSettings SmartBeaconing,
    GpsdSettings Gpsd,
    FrequencyReferenceSettings FrequencyReference,
    NetScriptSettings NetScripts)
{
    public const int CurrentSchemaVersion = 2;

    public static AppSettings Default { get; } = new(
        SchemaVersion: CurrentSchemaVersion,
        Station: StationProfile.Default,
        Connections: ConnectionSettings.Default,
        IGate: IGateSettings.Default,
        Digipeater: DigipeaterSettings.Default,
        Audio: AudioSettings.Default,
        Windows: WindowStates.Default,
        Gps: GpsSettings.Default,
        ManagedModem: ManagedModemSettings.Default,
        DarkMode: false,
        MessageTemplates: MessageTemplatesSettings.Default,
        SmartBeaconing: SmartBeaconingSettings.Default,
        Gpsd: GpsdSettings.Default,
        FrequencyReference: FrequencyReferenceSettings.Default,
        NetScripts: NetScriptSettings.Default);
}
