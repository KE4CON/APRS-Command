namespace AprsCommand.Contracts;

public sealed record PortStatusDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? PortId { get; init; }
    public string? PortName { get; init; }
    public string? PortType { get; init; }
    public bool Connected { get; init; }
    public DateTimeOffset? LastConnectedTime { get; init; }
    public DateTimeOffset? LastDisconnectedTime { get; init; }
    public int PacketsReceived { get; init; }
    public int PacketsTransmitted { get; init; }
    public string? LastError { get; init; }
}
