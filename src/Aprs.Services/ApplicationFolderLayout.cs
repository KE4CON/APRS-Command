namespace Aprs.Services;

public sealed record ApplicationFolderLayout(
    string RootFolderPath,
    string ConfigFolderPath,
    string LogsFolderPath,
    string PacketLogsFolderPath,
    string EventLogsFolderPath,
    string MapsFolderPath,
    string MapCacheFolderPath,
    string ExportsFolderPath,
    string FileHooksFolderPath,
    string FileHooksIncomingFolderPath,
    string FileHooksProcessedFolderPath,
    string FileHooksRejectedFolderPath,
    string FileHooksExportsFolderPath,
    string PluginsFolderPath,
    string BackupsFolderPath,
    string TrainingFolderPath,
    string ReplayFolderPath)
{
    public IReadOnlyList<string> AllFolders =>
    [
        RootFolderPath,
        ConfigFolderPath,
        LogsFolderPath,
        PacketLogsFolderPath,
        EventLogsFolderPath,
        MapsFolderPath,
        MapCacheFolderPath,
        ExportsFolderPath,
        FileHooksFolderPath,
        FileHooksIncomingFolderPath,
        FileHooksProcessedFolderPath,
        FileHooksRejectedFolderPath,
        FileHooksExportsFolderPath,
        PluginsFolderPath,
        BackupsFolderPath,
        TrainingFolderPath,
        ReplayFolderPath
    ];

    public static ApplicationFolderLayout FromRoot(string rootFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolderPath);

        var root = Path.GetFullPath(rootFolderPath);
        var fileHooks = Path.Combine(root, "file-hooks");

        return new ApplicationFolderLayout(
            RootFolderPath: root,
            ConfigFolderPath: Path.Combine(root, "config"),
            LogsFolderPath: Path.Combine(root, "logs"),
            PacketLogsFolderPath: Path.Combine(root, "packet-logs"),
            EventLogsFolderPath: Path.Combine(root, "event-logs"),
            MapsFolderPath: Path.Combine(root, "maps"),
            MapCacheFolderPath: Path.Combine(root, "map-cache"),
            ExportsFolderPath: Path.Combine(root, "exports"),
            FileHooksFolderPath: fileHooks,
            FileHooksIncomingFolderPath: Path.Combine(fileHooks, "incoming"),
            FileHooksProcessedFolderPath: Path.Combine(fileHooks, "processed"),
            FileHooksRejectedFolderPath: Path.Combine(fileHooks, "rejected"),
            FileHooksExportsFolderPath: Path.Combine(fileHooks, "exports"),
            PluginsFolderPath: Path.Combine(root, "plugins"),
            BackupsFolderPath: Path.Combine(root, "backups"),
            TrainingFolderPath: Path.Combine(root, "training"),
            ReplayFolderPath: Path.Combine(root, "replay"));
    }

    public static string GetDefaultApplicationDataFolder()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = AppContext.BaseDirectory;
        }

        return Path.Combine(baseFolder, "APRS Command");
    }
}
