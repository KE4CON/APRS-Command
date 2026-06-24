using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed record FileHookImportResult(
    bool Success,
    FileHookImportKind ImportKind,
    int AcceptedCount = 0,
    int RejectedCount = 0,
    string? Error = null,
    IReadOnlyList<ValidationMessageDto>? ValidationMessages = null)
{
    public static FileHookImportResult Accepted(FileHookImportKind kind, int count)
    {
        return new FileHookImportResult(true, kind, count);
    }

    public static FileHookImportResult Rejected(FileHookImportKind kind, string error, IReadOnlyList<ValidationMessageDto>? messages = null)
    {
        return new FileHookImportResult(false, kind, RejectedCount: 1, Error: error, ValidationMessages: messages);
    }
}
