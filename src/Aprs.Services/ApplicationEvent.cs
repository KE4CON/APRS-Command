namespace Aprs.Services;

public sealed record ApplicationEvent(
    Guid EventId,
    ApplicationEventType EventType,
    DateTimeOffset TimestampUtc,
    SourceMetadata Source,
    string? EntityId = null,
    string? Summary = null,
    IReadOnlyDictionary<string, string>? Attributes = null,
    IReadOnlyList<string>? ValidationWarnings = null,
    IReadOnlyList<string>? ValidationErrors = null)
{
    public static ApplicationEvent Create(
        ApplicationEventType eventType,
        DateTimeOffset timestampUtc,
        SourceMetadata? source = null,
        string? entityId = null,
        string? summary = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<string>? validationWarnings = null,
        IReadOnlyList<string>? validationErrors = null)
    {
        return new ApplicationEvent(
            Guid.NewGuid(),
            eventType,
            timestampUtc,
            source ?? SourceMetadata.Unknown(timestampUtc),
            entityId,
            summary,
            attributes,
            validationWarnings,
            validationErrors);
    }
}
