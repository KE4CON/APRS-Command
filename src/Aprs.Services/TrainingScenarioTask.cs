namespace Aprs.Services;

public sealed record TrainingScenarioTask(
    Guid TaskId,
    string Title,
    string Description,
    TrainingTaskStatus Status);
