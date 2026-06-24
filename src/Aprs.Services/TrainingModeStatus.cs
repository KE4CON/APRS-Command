namespace Aprs.Services;

public sealed record TrainingModeStatus(
    TrainingModeState State,
    bool TrainingModeEnabled,
    bool TransmitDisabled,
    TrainingScenario? SelectedScenario,
    int CompletedTaskCount,
    int TotalTaskCount,
    double ProgressPercent,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastUpdatedAtUtc,
    string? LastEvent,
    string? LastError);
