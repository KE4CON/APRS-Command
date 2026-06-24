namespace AprsCommand.Contracts;

public sealed record ExternalSourceMetadata(
    string? SourceName = null,
    ExternalSourceType SourceType = ExternalSourceType.Unknown,
    string? SourceId = null,
    DateTimeOffset? Timestamp = null,
    ContractDataOrigin Origin = ContractDataOrigin.Unknown,
    ExternalTrustLevel TrustLevel = ExternalTrustLevel.Untrusted);
