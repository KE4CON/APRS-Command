namespace AprsCommand.Contracts;

public sealed record MessageDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? MessageId { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Text { get; init; }
    public bool AckRequested { get; init; }
    public string? AckId { get; init; }
    public DateTimeOffset? ReceivedTimestamp { get; init; }
    public DateTimeOffset? SentTimestamp { get; init; }
    public string? MessageState { get; init; }
}
