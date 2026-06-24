namespace AprsCommand.Contracts;

public sealed record WeatherObservationDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? StationId { get; init; }
    public string? Callsign { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Temperature { get; init; }
    public double? Humidity { get; init; }
    public double? Pressure { get; init; }
    public double? WindDirection { get; init; }
    public double? WindSpeed { get; init; }
    public double? WindGust { get; init; }
    public double? RainLastHour { get; init; }
    public double? RainLast24Hours { get; init; }
    public double? RainSinceMidnight { get; init; }
    public double? Luminosity { get; init; }
    public double? UvIndex { get; init; }
    public double? Snow { get; init; }
    public DateTimeOffset? ObservationTime { get; init; }
}
