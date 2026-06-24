namespace AprsCommand.Api;

public sealed record FileHookConfiguration
{
    public bool FileHooksEnabled { get; init; }
    public bool ImportEnabled { get; init; }
    public bool ExportEnabled { get; init; }
    public string BaseFolderPath { get; init; } = "file-hooks";
    public string ImportFolderPath { get; init; } = "file-hooks/incoming";
    public string ExportFolderPath { get; init; } = "file-hooks/exports";
    public bool ArchiveProcessedImports { get; init; } = true;
    public bool RejectInvalidImports { get; init; } = true;
    public long MaximumImportFileSizeBytes { get; init; } = 1_048_576;
    public long MaximumExportFileSizeBytes { get; init; } = 10_485_760;
    public TimeSpan ImportPollingInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan AutoExportInterval { get; init; } = TimeSpan.FromMinutes(15);
    public bool IncludeStationsExport { get; init; } = true;
    public bool IncludeWeatherExport { get; init; } = true;
    public bool IncludeObjectsExport { get; init; } = true;
    public bool IncludeMessagesExport { get; init; } = true;
    public bool IncludeAlertsExport { get; init; } = true;
    public bool IncludeRawPacketsExport { get; init; } = true;
    public bool IncludeDecodedEventsExport { get; init; } = true;
    public bool IncludeDiagnosticsExport { get; init; } = true;
    public bool AllowImportedStationData { get; init; }
    public bool AllowImportedWeatherData { get; init; }
    public bool AllowImportedObjectData { get; init; }
    public bool AllowImportedGpsData { get; init; }
    public bool AllowImportedRawPacketData { get; init; }
    public bool AllowImportedTransmitRequests { get; init; }
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTimestamp { get; init; } = DateTimeOffset.UtcNow;

    public bool HasTransmitCapability => false;

    public static FileHookConfiguration Default { get; } = new();
}
