namespace AprsCommand.Api;

public sealed record FileHookStatus(
    FileHookState State,
    bool FileHooksEnabled,
    bool ImportEnabled,
    bool ExportEnabled,
    string BaseFolderPath,
    string ImportFolderPath,
    string ExportFolderPath,
    DateTimeOffset? LastExportTime = null,
    DateTimeOffset? LastImportTime = null,
    int AcceptedImportCount = 0,
    int RejectedImportCount = 0,
    string? LastError = null);
