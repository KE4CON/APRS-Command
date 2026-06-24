namespace Aprs.Services;

public interface ITrainingModeService
{
    TrainingModeConfiguration Configuration { get; }

    void EnableTrainingMode();

    void DisableTrainingMode();

    IReadOnlyList<TrainingScenario> ListScenarios();

    TrainingScenario? GetSelectedScenario();

    bool SelectScenario(Guid scenarioId);

    Task StartSelectedScenarioAsync(CancellationToken cancellationToken = default);

    void PauseScenario();

    void ResumeScenario();

    void StopScenario();

    void ResetScenario();

    bool SetTaskStatus(Guid taskId, TrainingTaskStatus status);

    TrainingModeStatus GetStatus();

    IReadOnlyList<TrainingScenarioTask> GetCurrentTasks();

    IReadOnlyList<TrainingEventRecord> GetRecentEvents(int? maximumCount = null);
}
