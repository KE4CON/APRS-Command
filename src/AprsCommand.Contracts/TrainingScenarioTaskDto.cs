namespace AprsCommand.Contracts;

public sealed record TrainingScenarioTaskDto(
    string TaskId = "",
    string Name = "",
    string? Description = null,
    bool Completed = false);
