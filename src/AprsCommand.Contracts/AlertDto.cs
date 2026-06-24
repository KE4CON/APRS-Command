namespace AprsCommand.Contracts;

public sealed record AlertDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? AlertId { get; init; }
    public string? RuleId { get; init; }
    public string? AlertType { get; init; }
    public string? Severity { get; init; }
    public string? Summary { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? TriggeredTime { get; init; }
    public bool Acknowledged { get; init; }
    public DateTimeOffset? AcknowledgedTime { get; init; }
}
