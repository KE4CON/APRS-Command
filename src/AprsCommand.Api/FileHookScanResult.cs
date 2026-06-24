namespace AprsCommand.Api;

public sealed record FileHookScanResult(
    bool Success,
    int FilesProcessed = 0,
    int AcceptedCount = 0,
    int RejectedCount = 0,
    string? Error = null);
