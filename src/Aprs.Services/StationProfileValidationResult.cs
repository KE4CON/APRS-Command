namespace Aprs.Services;

public sealed record StationProfileValidationResult(
    bool IsValid,
    bool IsSafeToTransmit,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
