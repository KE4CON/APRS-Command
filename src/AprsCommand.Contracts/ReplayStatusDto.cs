namespace AprsCommand.Contracts;

public sealed record ReplayStatusDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public bool Enabled { get; init; }
    public string? ReplayState { get; init; }
    public int CurrentPosition { get; init; }
    public int TotalEntries { get; init; }
    public DateTimeOffset? CurrentReplayTimestamp { get; init; }
    public double SpeedMultiplier { get; init; } = 1;
    public bool TransmitDisabled { get; init; } = true;
}
