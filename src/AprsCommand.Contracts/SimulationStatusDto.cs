namespace AprsCommand.Contracts;

public sealed record SimulationStatusDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public bool Enabled { get; init; }
    public bool Running { get; init; }
    public int SimulatedStationCount { get; init; }
    public int SimulatedObjectCount { get; init; }
    public int SimulatedWeatherStationCount { get; init; }
    public DateTimeOffset? LastUpdate { get; init; }
}
