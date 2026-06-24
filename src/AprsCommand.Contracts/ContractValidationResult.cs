namespace AprsCommand.Contracts;

public sealed record ContractValidationResult(
    bool IsValid,
    List<ValidationMessageDto>? Warnings = null,
    List<ValidationMessageDto>? Errors = null)
{
    public static ContractValidationResult Valid { get; } = new(true, [], []);
}
