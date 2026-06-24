namespace Aprs.Services;

public sealed record RawPacketLogConfiguration(
    bool RawPacketLoggingEnabled,
    int MaximumInMemoryEntries,
    bool FileLoggingEnabled,
    string? LogFolderPath,
    bool RotateLogs,
    long MaximumLogFileSizeBytes,
    bool IncludeReceivedPackets,
    bool IncludeTransmittedPackets,
    bool IncludeBlockedTransmitAttempts,
    bool IncludeGeneratedPackets)
{
    public static RawPacketLogConfiguration Default { get; } = new(
        RawPacketLoggingEnabled: true,
        MaximumInMemoryEntries: 5000,
        FileLoggingEnabled: false,
        LogFolderPath: null,
        RotateLogs: true,
        MaximumLogFileSizeBytes: 10 * 1024 * 1024,
        IncludeReceivedPackets: true,
        IncludeTransmittedPackets: true,
        IncludeBlockedTransmitAttempts: true,
        IncludeGeneratedPackets: true);
}
