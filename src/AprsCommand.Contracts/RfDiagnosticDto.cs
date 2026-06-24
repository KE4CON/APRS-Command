namespace AprsCommand.Contracts;

public sealed record RfDiagnosticDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? PacketId { get; init; }
    public string? Callsign { get; init; }
    public double? PacketRate { get; init; }
    public int DuplicateCount { get; init; }
    public List<string> PathWarnings { get; init; } = [];
    public List<string> HeardVia { get; init; } = [];
    public bool SeenOnRf { get; init; }
    public bool SeenOnAprsIs { get; init; }
}
