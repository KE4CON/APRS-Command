namespace Aprs.Services;

public sealed record AprsPortTransmitSafetyResult(
    bool IsSafe,
    string? FailureReason,
    AprsPortSnapshot? Port);
