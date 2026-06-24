using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed record WebSocketEventStreamEnvelope
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string StreamName { get; init; } = "events";
    public string EventType { get; init; } = string.Empty;
    public string EventCategory { get; init; } = string.Empty;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public string PayloadType { get; init; } = nameof(DecodedEventDto);
    public object? Payload { get; init; }
    public List<ValidationMessageDto> Warnings { get; init; } = [];
    public List<ValidationMessageDto> Errors { get; init; } = [];
}
