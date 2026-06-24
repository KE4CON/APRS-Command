namespace Aprs.Services;

public sealed record AprsMessageComposeValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static AprsMessageComposeValidationResult Success { get; } = new(
        IsValid: true,
        Errors: []);
}
