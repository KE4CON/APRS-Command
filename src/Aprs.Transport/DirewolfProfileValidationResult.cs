namespace Aprs.Transport;

public sealed record DirewolfProfileValidationResult(
    bool IsValid,
    bool IsSafeForReceive,
    bool IsSafeForTransmit,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
