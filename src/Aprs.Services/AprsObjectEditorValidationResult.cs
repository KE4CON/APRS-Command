namespace Aprs.Services;

public sealed record AprsObjectEditorValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
