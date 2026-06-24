using AprsCommand.Api;

namespace Aprs.Desktop.ViewModels;

public sealed class FileHooksViewModel
{
    private readonly IFileHookService service;

    public FileHooksViewModel(IFileHookService service)
    {
        this.service = service;
        ManualExportCommand = new DesktopCommand(ManualExport);
        ManualImportScanCommand = new DesktopCommand(ManualImportScan);
        ClearStatusCommand = new DesktopCommand(ClearStatus);
        Refresh();
    }

    public bool FileHooksEnabled { get; private set; }
    public bool ImportEnabled { get; private set; }
    public bool ExportEnabled { get; private set; }
    public string State { get; private set; } = string.Empty;
    public string BaseFolderPath { get; private set; } = string.Empty;
    public string ImportFolderPath { get; private set; } = string.Empty;
    public string ExportFolderPath { get; private set; } = string.Empty;
    public string LastExportTime { get; private set; } = "-";
    public string LastImportTime { get; private set; } = "-";
    public int AcceptedImportCount { get; private set; }
    public int RejectedImportCount { get; private set; }
    public string ImportCountSummary { get; private set; } = "Accepted 0, rejected 0";
    public string LastError { get; private set; } = "-";
    public string LastAction { get; private set; } = "Ready";

    public DesktopCommand ManualExportCommand { get; }
    public DesktopCommand ManualImportScanCommand { get; }
    public DesktopCommand ClearStatusCommand { get; }

    public static FileHooksViewModel CreateDesignTime()
    {
        var configuration = FileHookConfiguration.Default with
        {
            BaseFolderPath = "file-hooks",
            ImportFolderPath = "file-hooks/incoming",
            ExportFolderPath = "file-hooks/exports"
        };
        return new FileHooksViewModel(new FileHookService(configuration));
    }

    private void ManualExport()
    {
        var results = service.ExportAllAsync().GetAwaiter().GetResult();
        var successCount = results.Count(result => result.Success);
        LastAction = successCount == results.Count
            ? $"Exported {successCount} file set(s)."
            : $"Export completed with {results.Count - successCount} error(s).";
        Refresh();
    }

    private void ManualImportScan()
    {
        var result = service.ScanImportFolderAsync().GetAwaiter().GetResult();
        LastAction = result.Success
            ? $"Scanned imports: {result.FilesProcessed} file(s), {result.AcceptedCount} accepted, {result.RejectedCount} rejected."
            : $"Import scan failed: {result.Error}";
        Refresh();
    }

    private void ClearStatus()
    {
        service.ClearStatus();
        LastAction = "Status cleared.";
        Refresh();
    }

    private void Refresh()
    {
        var status = service.Status;
        FileHooksEnabled = status.FileHooksEnabled;
        ImportEnabled = status.ImportEnabled;
        ExportEnabled = status.ExportEnabled;
        State = status.State.ToString();
        BaseFolderPath = status.BaseFolderPath;
        ImportFolderPath = status.ImportFolderPath;
        ExportFolderPath = status.ExportFolderPath;
        LastExportTime = status.LastExportTime?.ToLocalTime().ToString("g") ?? "-";
        LastImportTime = status.LastImportTime?.ToLocalTime().ToString("g") ?? "-";
        AcceptedImportCount = status.AcceptedImportCount;
        RejectedImportCount = status.RejectedImportCount;
        ImportCountSummary = $"Accepted {AcceptedImportCount}, rejected {RejectedImportCount}";
        LastError = string.IsNullOrWhiteSpace(status.LastError) ? "-" : status.LastError;
    }
}
