namespace AprsCommand.Contracts;

public sealed record GpsPositionDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Altitude { get; init; }
    public double? Speed { get; init; }
    public double? Course { get; init; }
    public string? FixQuality { get; init; }
    public int? Satellites { get; init; }
    public int? UsedSatellites { get; init; }
    public double? Hdop { get; init; }
    public bool FixValid { get; init; }
}
