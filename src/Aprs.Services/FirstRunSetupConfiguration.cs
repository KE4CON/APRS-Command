namespace Aprs.Services;

public sealed record FirstRunSetupConfiguration(
    bool FirstRunCompleted,
    string ApplicationDataFolderPath,
    string LogsFolderPath,
    string MapCacheFolderPath,
    string PacketLogFolderPath,
    string ExportFolderPath,
    string FileHooksFolderPath,
    string PluginFolderPath,
    bool StationProfileConfigured,
    bool SafetySettingsReviewed,
    bool AprsIsSettingsReviewed,
    bool RfTncSettingsReviewed,
    bool MapSettingsReviewed,
    bool TransmitEnabled,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    bool IGateEnabled,
    bool DigipeaterEnabled,
    bool BeaconingEnabled,
    bool WeatherBeaconingEnabled,
    bool RestApiEnabled,
    bool WebSocketEnabled,
    bool FileHooksEnabled,
    bool PluginLoadingEnabled,
    DateTimeOffset CreatedTimestampUtc,
    DateTimeOffset UpdatedTimestampUtc)
{
    public static FirstRunSetupConfiguration CreateDefault(DateTimeOffset now, string? applicationDataFolderPath = null)
    {
        var layout = ApplicationFolderLayout.FromRoot(
            string.IsNullOrWhiteSpace(applicationDataFolderPath)
                ? ApplicationFolderLayout.GetDefaultApplicationDataFolder()
                : applicationDataFolderPath);

        return new FirstRunSetupConfiguration(
            FirstRunCompleted: false,
            ApplicationDataFolderPath: layout.RootFolderPath,
            LogsFolderPath: layout.LogsFolderPath,
            MapCacheFolderPath: layout.MapCacheFolderPath,
            PacketLogFolderPath: layout.PacketLogsFolderPath,
            ExportFolderPath: layout.ExportsFolderPath,
            FileHooksFolderPath: layout.FileHooksFolderPath,
            PluginFolderPath: layout.PluginsFolderPath,
            StationProfileConfigured: false,
            SafetySettingsReviewed: false,
            AprsIsSettingsReviewed: false,
            RfTncSettingsReviewed: false,
            MapSettingsReviewed: false,
            TransmitEnabled: false,
            AprsIsTransmitEnabled: false,
            RfTransmitEnabled: false,
            IGateEnabled: false,
            DigipeaterEnabled: false,
            BeaconingEnabled: false,
            WeatherBeaconingEnabled: false,
            RestApiEnabled: false,
            WebSocketEnabled: false,
            FileHooksEnabled: false,
            PluginLoadingEnabled: false,
            CreatedTimestampUtc: now,
            UpdatedTimestampUtc: now);
    }
}
