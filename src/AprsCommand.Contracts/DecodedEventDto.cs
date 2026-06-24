namespace AprsCommand.Contracts;

public sealed record DecodedEventDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? EventId { get; init; }
    public string? EventType { get; init; }
    public string? Category { get; init; }
    public string? Severity { get; init; }
    public string? Summary { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? EventTime { get; init; }
    public string? RelatedCallsign { get; init; }
}
