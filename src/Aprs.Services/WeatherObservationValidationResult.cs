namespace Aprs.Services;

public sealed record WeatherObservationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
