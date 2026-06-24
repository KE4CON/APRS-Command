namespace AprsCommand.Contracts;

public sealed record AprsObjectDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? ObjectName { get; init; }
    public string? ObjectType { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? SymbolTable { get; init; }
    public string? SymbolCode { get; init; }
    public string? Comment { get; init; }
    public bool Active { get; init; } = true;
    public bool Killed { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedTime { get; init; }
}
