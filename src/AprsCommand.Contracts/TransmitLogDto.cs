namespace AprsCommand.Contracts;

public sealed record TransmitLogDto : IContractDto
{
    public string SchemaVersion { get; init; } = ContractSchemaVersion.Current;
    public ExternalSourceMetadata SourceMetadata { get; init; } = new();
    public DateTimeOffset? Timestamp { get; init; }
    public List<ValidationMessageDto> ValidationWarnings { get; init; } = [];
    public List<ValidationMessageDto> ValidationErrors { get; init; } = [];
    public string? Notes { get; init; }
    public string? TransmitId { get; init; }
    public string? PacketText { get; init; }
    public string? TransmitType { get; init; }
    public string? RequestedBy { get; init; }
    public ExtensionPermission PermissionUsed { get; init; } = ExtensionPermission.ReadOnly;
    public bool Allowed { get; init; }
    public bool Blocked => !Allowed;
    public string? BlockReason { get; init; }
    public DateTimeOffset? TransmitTime { get; init; }
    public string? Result { get; init; }
}
