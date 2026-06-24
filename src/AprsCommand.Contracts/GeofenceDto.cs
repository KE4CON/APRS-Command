namespace AprsCommand.Contracts;

public sealed record GeofenceDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? GeofenceId { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public bool Enabled { get; init; }
    public double? CenterLatitude { get; init; }
    public double? CenterLongitude { get; init; }
    public double? Radius { get; init; }
    public List<GeoPointDto> PolygonPoints { get; init; } = [];
    public bool AlertOnEnter { get; init; }
    public bool AlertOnExit { get; init; }
}
