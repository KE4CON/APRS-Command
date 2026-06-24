namespace Aprs.Services;

public sealed record SourceMetadata(
    string? SourceName,
    DataSourceType SourceType,
    string? SourceId,
    DateTimeOffset TimestampUtc,
    DataOrigin Origin,
    SourceTrustLevel TrustLevel)
{
    public static SourceMetadata Unknown(DateTimeOffset timestampUtc)
    {
        return new SourceMetadata(null, DataSourceType.Unknown, null, timestampUtc, DataOrigin.Unknown, SourceTrustLevel.Untrusted);
    }

    public static SourceMetadata Create(
        string? sourceName,
        DataSourceType sourceType,
        string? sourceId,
        DateTimeOffset timestampUtc,
        DataOrigin origin,
        SourceTrustLevel trustLevel = SourceTrustLevel.Untrusted)
    {
        return new SourceMetadata(
            string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim(),
            sourceType,
            string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim(),
            timestampUtc,
            origin,
            trustLevel);
    }
}
