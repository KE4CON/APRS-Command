namespace AprsCommand.Contracts;

public sealed record RawPacketDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? RawPacket { get; init; }
    public string? ParsedPacketType { get; init; }
    public string? SourceCallsign { get; init; }
    public string? Destination { get; init; }
    public List<string> Path { get; init; } = [];
    public ContractDirection Direction { get; init; } = ContractDirection.Unknown;
    public DateTimeOffset? ReceivedTime { get; init; }
}
