namespace Aprs.Services;

public sealed class TrainingModeService : ITrainingModeService
{
    private readonly ISimulationService? simulationService;
    private readonly IReplayService? replayService;
    private readonly IBeaconSchedulerClock clock;
    private readonly IDecodedEventLogService? decodedEventLog;
    private readonly List<TrainingScenario> scenarios;
    private readonly List<TrainingEventRecord> events = [];
    private TrainingModeState state = TrainingModeState.Disabled;
    private TrainingScenario? selectedScenario;
    private DateTimeOffset? startedAtUtc;
    private string? lastError;

    public TrainingModeService(
        TrainingModeConfiguration? configuration = null,
        ISimulationService? simulationService = null,
        IReplayService? replayService = null,
        IDecodedEventLogService? decodedEventLog = null,
        IBeaconSchedulerClock? clock = null,
        IReadOnlyList<TrainingScenario>? scenarios = null)
    {
        Configuration = Normalize(configuration ?? TrainingModeConfiguration.Default);
        this.simulationService = simulationService;
        this.replayService = replayService;
        this.decodedEventLog = decodedEventLog;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.scenarios = (scenarios ?? CreateBuiltInScenarios()).ToList();
        selectedScenario = SelectInitialScenario();
        state = Configuration.TrainingModeEnabled ? TrainingModeState.Ready : TrainingModeState.Disabled;
    }

    public TrainingModeConfiguration Configuration { get; private set; }

    public void EnableTrainingMode()
    {
        Configuration = Configuration with { TrainingModeEnabled = true, UpdatedAtUtc = clock.UtcNow };
        state = TrainingModeState.Ready;
        AddEvent("Training mode enabled.", null);
    }

    public void DisableTrainingMode()
    {
        StopScenario();
        Configuration = Configuration with { TrainingModeEnabled = false, UpdatedAtUtc = clock.UtcNow };
        state = TrainingModeState.Disabled;
        AddEvent("Training mode disabled.", null);
    }

    public IReadOnlyList<TrainingScenario> ListScenarios()
    {
        return scenarios.ToArray();
    }

    public TrainingScenario? GetSelectedScenario()
    {
        return selectedScenario;
    }

    public bool SelectScenario(Guid scenarioId)
    {
        var scenario = scenarios.FirstOrDefault(item => item.ScenarioId == scenarioId);
        if (scenario is null)
        {
            return false;
        }

        selectedScenario = ResetTasks(scenario);
        Configuration = Configuration with
        {
            SelectedScenarioId = selectedScenario.ScenarioId,
            SelectedScenarioName = selectedScenario.ScenarioName,
            UpdatedAtUtc = clock.UtcNow
        };
        state = Configuration.TrainingModeEnabled ? TrainingModeState.Ready : TrainingModeState.Disabled;
        AddEvent($"Selected training scenario: {selectedScenario.ScenarioName}.", selectedScenario.Description);
        return true;
    }

    public async Task StartSelectedScenarioAsync(CancellationToken cancellationToken = default)
    {
        if (!Configuration.TrainingModeEnabled)
        {
            state = TrainingModeState.Disabled;
            return;
        }

        if (selectedScenario is null)
        {
            state = TrainingModeState.Faulted;
            lastError = "No training scenario is selected.";
            return;
        }

        state = TrainingModeState.Starting;
        startedAtUtc = clock.UtcNow;
        AddEvent($"Starting training scenario: {selectedScenario.ScenarioName}.", selectedScenario.Description);

        try
        {
            if (Configuration.UseSimulatedAprsSource && Configuration.AutoStartSimulation && simulationService is not null)
            {
                await simulationService.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            if (Configuration.UseReplaySource && Configuration.AutoStartReplay && replayService is not null)
            {
                await replayService.StartReplayAsync(cancellationToken).ConfigureAwait(false);
            }

            state = TrainingModeState.Running;
            AddEvent($"Training scenario running: {selectedScenario.ScenarioName}.", null);
        }
        catch (OperationCanceledException)
        {
            state = TrainingModeState.Stopped;
            throw;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            state = TrainingModeState.Faulted;
            AddEvent("Training scenario faulted.", ex.Message);
        }
    }

    public void PauseScenario()
    {
        if (state != TrainingModeState.Running)
        {
            return;
        }

        simulationService?.Pause();
        replayService?.Pause();
        state = TrainingModeState.Paused;
        AddEvent("Training scenario paused.", selectedScenario?.ScenarioName);
    }

    public void ResumeScenario()
    {
        if (state != TrainingModeState.Paused)
        {
            return;
        }

        simulationService?.Resume();
        replayService?.Resume();
        state = TrainingModeState.Running;
        AddEvent("Training scenario resumed.", selectedScenario?.ScenarioName);
    }

    public void StopScenario()
    {
        simulationService?.Stop();
        replayService?.Stop();
        if (state != TrainingModeState.Disabled)
        {
            state = TrainingModeState.Stopped;
        }

        AddEvent("Training scenario stopped.", selectedScenario?.ScenarioName);
    }

    public void ResetScenario()
    {
        simulationService?.Reset();
        replayService?.Stop();
        if (selectedScenario is not null)
        {
            selectedScenario = ResetTasks(selectedScenario);
        }

        startedAtUtc = null;
        lastError = null;
        state = Configuration.TrainingModeEnabled ? TrainingModeState.Ready : TrainingModeState.Disabled;
        AddEvent("Training scenario reset.", selectedScenario?.ScenarioName);
    }

    public bool SetTaskStatus(Guid taskId, TrainingTaskStatus status)
    {
        if (selectedScenario is null)
        {
            return false;
        }

        var index = selectedScenario.ExpectedTrainingTasks.ToList().FindIndex(task => task.TaskId == taskId);
        if (index < 0)
        {
            return false;
        }

        var tasks = selectedScenario.ExpectedTrainingTasks.ToArray();
        tasks[index] = tasks[index] with { Status = status };
        selectedScenario = selectedScenario with
        {
            ExpectedTrainingTasks = tasks,
            UpdatedAtUtc = clock.UtcNow
        };

        if (tasks.Length > 0 && tasks.All(task => task.Status is TrainingTaskStatus.Completed or TrainingTaskStatus.Skipped))
        {
            state = TrainingModeState.Completed;
        }

        AddEvent($"Training task updated: {tasks[index].Title}.", status.ToString());
        return true;
    }

    public TrainingModeStatus GetStatus()
    {
        var tasks = selectedScenario?.ExpectedTrainingTasks ?? [];
        var completed = tasks.Count(task => task.Status == TrainingTaskStatus.Completed);
        var progress = tasks.Count == 0 ? 0 : completed * 100.0 / tasks.Count;
        return new TrainingModeStatus(
            state,
            Configuration.TrainingModeEnabled,
            Configuration.TransmitDisabled,
            selectedScenario,
            completed,
            tasks.Count,
            progress,
            startedAtUtc,
            events.LastOrDefault()?.TimestampUtc,
            events.LastOrDefault()?.Summary,
            lastError);
    }

    public IReadOnlyList<TrainingScenarioTask> GetCurrentTasks()
    {
        return selectedScenario?.ExpectedTrainingTasks.ToArray() ?? [];
    }

    public IReadOnlyList<TrainingEventRecord> GetRecentEvents(int? maximumCount = null)
    {
        var query = events.OrderByDescending(item => item.TimestampUtc).ThenByDescending(item => item.EventId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    public static IReadOnlyList<TrainingScenario> CreateBuiltInScenarios()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            CreateScenario("Beginner APRS map familiarization", TrainingScenarioDifficulty.Beginner, TrainingScenarioType.BasicMapFamiliarization, "Find simulated stations and inspect map markers.", ["Locate SIM001 on the map", "Open station details", "Identify packet source"], now),
            CreateScenario("Track simulated mobile stations", TrainingScenarioDifficulty.Beginner, TrainingScenarioType.StationTracking, "Follow moving simulated stations and compare trails.", ["Start simulation", "Find SIM001-9", "Observe movement"], now),
            CreateScenario("Practice APRS messaging", TrainingScenarioDifficulty.Intermediate, TrainingScenarioType.MessagingPractice, "Review simulated incoming message traffic.", ["Open message center", "Find simulated message", "Mark practice response placeholder"], now),
            CreateScenario("Monitor simulated weather station", TrainingScenarioDifficulty.Beginner, TrainingScenarioType.WeatherMonitoring, "Watch simulated weather values and status.", ["Open weather panel", "Find TESTWX1", "Check wind gust"], now),
            CreateScenario("Object placement practice", TrainingScenarioDifficulty.Intermediate, TrainingScenarioType.ObjectManagement, "Create and edit local APRS objects safely.", ["Create object", "Edit object comment", "Review object list"], now),
            CreateScenario("Geofence entry/exit practice", TrainingScenarioDifficulty.Intermediate, TrainingScenarioType.GeofenceExercise, "Use simulated movement with geofence alerts.", ["Create geofence", "Observe enter event", "Observe exit event"], now),
            CreateScenario("Alert response practice", TrainingScenarioDifficulty.Advanced, TrainingScenarioType.AlertResponse, "Review alert history and acknowledge triggers.", ["Open alerts", "Acknowledge alert", "Clear practice history"], now),
            CreateScenario("Replay review practice", TrainingScenarioDifficulty.Intermediate, TrainingScenarioType.ReplayReview, "Load and review replayed packet logs.", ["Open replay", "Load replay file placeholder", "Step through packets"], now)
        ];
    }

    private TrainingScenario? SelectInitialScenario()
    {
        if (Configuration.SelectedScenarioId is not null)
        {
            return scenarios.FirstOrDefault(scenario => scenario.ScenarioId == Configuration.SelectedScenarioId);
        }

        return scenarios.FirstOrDefault();
    }

    private void AddEvent(string summary, string? details)
    {
        var timestamp = clock.UtcNow;
        events.Add(new TrainingEventRecord(Guid.NewGuid(), timestamp, state, summary, details));
        decodedEventLog?.AddEvent(
            DecodedEventType.TrainingScenarioUpdated,
            DecodedEventCategory.System,
            DecodedEventSeverity.Info,
            summary,
            details,
            relatedEntity: selectedScenario?.ScenarioName,
            packetSource: AprsPacketSource.Simulation,
            timestampUtc: timestamp);
    }

    private static TrainingModeConfiguration Normalize(TrainingModeConfiguration configuration)
    {
        return configuration with
        {
            ScenarioSpeedMultiplier = configuration.ScenarioSpeedMultiplier <= 0 ? 1.0 : configuration.ScenarioSpeedMultiplier
        };
    }

    private static TrainingScenario ResetTasks(TrainingScenario scenario)
    {
        return scenario with
        {
            ExpectedTrainingTasks = scenario.ExpectedTrainingTasks
                .Select(task => task with { Status = TrainingTaskStatus.NotStarted })
                .ToArray()
        };
    }

    private static TrainingScenario CreateScenario(
        string name,
        TrainingScenarioDifficulty difficulty,
        TrainingScenarioType type,
        string description,
        IReadOnlyList<string> taskTitles,
        DateTimeOffset now)
    {
        return new TrainingScenario(
            Guid.NewGuid(),
            name,
            description,
            difficulty,
            TimeSpan.FromMinutes(difficulty == TrainingScenarioDifficulty.Advanced ? 45 : 20),
            type,
            SimulationConfiguration.Default with { SimulationEnabled = true },
            null,
            taskTitles.Select(title => new TrainingScenarioTask(Guid.NewGuid(), title, title, TrainingTaskStatus.NotStarted)).ToArray(),
            ["Complete or skip all listed practice tasks."],
            now,
            now,
            "Built-in offline training scenario using fake data only.");
    }
}
