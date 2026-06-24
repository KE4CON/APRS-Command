using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed record FileHookExportEnvelope<T>
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
    public string SourceApplicationName { get; init; } = "APRS Command";
    public string SourceApplicationVersion { get; init; } = "0.1";
    public int ItemCount { get; init; }
    public IReadOnlyList<T> Data { get; init; } = [];
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
}
