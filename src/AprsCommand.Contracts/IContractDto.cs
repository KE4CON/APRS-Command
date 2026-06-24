namespace AprsCommand.Contracts;

public interface IContractDto
{
    string SchemaVersion { get; }

    ExternalSourceMetadata SourceMetadata { get; }

    DateTimeOffset? Timestamp { get; }

    List<ValidationMessageDto> ValidationWarnings { get; }

    List<ValidationMessageDto> ValidationErrors { get; }

    string? Notes { get; }
}
