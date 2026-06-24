namespace Aprs.Services;

public sealed record DecodedEventLogConfiguration(
    bool DecodedEventLoggingEnabled,
    int MaximumInMemoryEvents,
    bool FileLoggingEnabled,
    string? LogFolderPath,
    bool RotateLogs,
    long MaximumLogFileSizeBytes)
{
    public static DecodedEventLogConfiguration Default { get; } = new(
        DecodedEventLoggingEnabled: true,
        MaximumInMemoryEvents: 5000,
        FileLoggingEnabled: false,
        LogFolderPath: null,
        RotateLogs: true,
        MaximumLogFileSizeBytes: 10 * 1024 * 1024);
}
