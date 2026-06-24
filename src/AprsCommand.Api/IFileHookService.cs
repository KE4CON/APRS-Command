namespace AprsCommand.Api;

public interface IFileHookService
{
    FileHookStatus Status { get; }

    Task<FileHookStatus> StartAsync(CancellationToken cancellationToken = default);
    Task<FileHookStatus> StopAsync(CancellationToken cancellationToken = default);
    void EnsureFolderStructure();
    Task<IReadOnlyList<FileHookExportResult>> ExportAllAsync(CancellationToken cancellationToken = default);
    Task<FileHookExportResult> ExportAsync(FileHookExportKind kind, CancellationToken cancellationToken = default);
    Task<FileHookImportResult> ImportAsync(FileHookImportKind kind, string content, CancellationToken cancellationToken = default);
    Task<FileHookScanResult> ScanImportFolderAsync(CancellationToken cancellationToken = default);
    void ClearStatus();
}
