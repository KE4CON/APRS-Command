namespace Aprs.Services;

public sealed record AprsEventEnvelope<TPayload>(
    AprsEventMetadata Metadata,
    TPayload? Payload = default,
    IReadOnlyDictionary<string, string>? Attributes = null) : AprsEventBase(Metadata);
