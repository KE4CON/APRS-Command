namespace AprsCommand.Contracts;

public sealed record ValidationMessageDto(
    ValidationSeverity Severity = ValidationSeverity.Warning,
    string Message = "",
    string? Code = null,
    string? FieldName = null);
