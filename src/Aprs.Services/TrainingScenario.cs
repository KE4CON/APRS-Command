namespace Aprs.Services;

public sealed record TrainingScenario(
    Guid ScenarioId,
    string ScenarioName,
    string Description,
    TrainingScenarioDifficulty Difficulty,
    TimeSpan EstimatedDuration,
    TrainingScenarioType ScenarioType,
    SimulationConfiguration? SimulationConfiguration,
    string? ReplayFilePath,
    IReadOnlyList<TrainingScenarioTask> ExpectedTrainingTasks,
    IReadOnlyList<string> CompletionCriteria,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Notes);
