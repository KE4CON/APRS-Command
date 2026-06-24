namespace Aprs.Services;

public sealed record TrainingEventRecord(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    TrainingModeState State,
    string Summary,
    string? Details);
