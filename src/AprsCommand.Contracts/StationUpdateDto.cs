namespace AprsCommand.Contracts;

public sealed record StationUpdateDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? Callsign { get; init; }
    public string? TacticalLabel { get; init; }
    public string? DisplayName { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Altitude { get; init; }
    public string? SymbolTable { get; init; }
    public string? SymbolCode { get; init; }
    public double? Course { get; init; }
    public double? Speed { get; init; }
    public string? StatusText { get; init; }
    public DateTimeOffset? LastHeard { get; init; }
}
