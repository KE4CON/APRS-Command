namespace AprsCommand.Api;

public sealed record FileHookExportResult(
    bool Success,
    FileHookExportKind ExportKind,
    string? FilePath = null,
    string? Content = null,
    int ItemCount = 0,
    string? Error = null)
{
    public static FileHookExportResult Ok(FileHookExportKind kind, string filePath, string content, int itemCount)
    {
        return new FileHookExportResult(true, kind, filePath, content, itemCount);
    }

    public static FileHookExportResult Failed(FileHookExportKind kind, string error)
    {
        return new FileHookExportResult(false, kind, Error: error);
    }
}
