namespace AprsCommand.Contracts;

public sealed record TrainingScenarioDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? ScenarioId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Difficulty { get; init; }
    public string? ScenarioType { get; init; }
    public TimeSpan? EstimatedDuration { get; init; }
    public List<TrainingScenarioTaskDto> Tasks { get; init; } = [];
}
