using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class TrainingModeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TrainingModeDisabledByDefaultAndTransmitDisabled()
    {
        var configuration = TrainingModeConfiguration.Default;
        var service = CreateService(configuration);

        Assert.False(configuration.TrainingModeEnabled);
        Assert.True(configuration.TransmitDisabled);
        Assert.Equal(TrainingModeState.Disabled, service.GetStatus().State);
    }

    [Fact]
    public void BuiltInScenariosAreListed()
    {
        var service = CreateService();

        var scenarios = service.ListScenarios();

        Assert.True(scenarios.Count >= 8);
        Assert.Contains(scenarios, scenario => scenario.ScenarioType == TrainingScenarioType.GeofenceExercise);
        Assert.Contains(scenarios, scenario => scenario.ScenarioType == TrainingScenarioType.ReplayReview);
    }

    [Fact]
    public void ScenarioCanBeSelected()
    {
        var service = CreateService();
        var scenario = service.ListScenarios().Last();

        var selected = service.SelectScenario(scenario.ScenarioId);

        Assert.True(selected);
        Assert.Equal(scenario.ScenarioId, service.GetSelectedScenario()?.ScenarioId);
    }

    [Fact]
    public async Task ScenarioCanBeStartedPausedResumedStoppedAndReset()
    {
        var service = CreateService(TrainingModeConfiguration.Default with { TrainingModeEnabled = true });

        await service.StartSelectedScenarioAsync();
        Assert.Equal(TrainingModeState.Running, service.GetStatus().State);

        service.PauseScenario();
        Assert.Equal(TrainingModeState.Paused, service.GetStatus().State);

        service.ResumeScenario();
        Assert.Equal(TrainingModeState.Running, service.GetStatus().State);

        service.StopScenario();
        Assert.Equal(TrainingModeState.Stopped, service.GetStatus().State);

        service.ResetScenario();
        Assert.Equal(TrainingModeState.Ready, service.GetStatus().State);
    }

    [Fact]
    public void TaskStatusCanBeChangedAndCompletedTasksAreCounted()
    {
        var service = CreateService(TrainingModeConfiguration.Default with { TrainingModeEnabled = true });
        var task = service.GetCurrentTasks().First();

        var changed = service.SetTaskStatus(task.TaskId, TrainingTaskStatus.Completed);

        Assert.True(changed);
        var status = service.GetStatus();
        Assert.Equal(1, status.CompletedTaskCount);
        Assert.True(status.ProgressPercent > 0);
    }

    [Fact]
    public void TrainingEventsAreLogged()
    {
        var eventLog = new DecodedEventLogService(clock: new FakeClock { UtcNow = Now });
        var service = CreateService(TrainingModeConfiguration.Default with { TrainingModeEnabled = true }, decodedEventLog: eventLog);

        service.EnableTrainingMode();

        Assert.NotEmpty(service.GetRecentEvents());
        Assert.NotEmpty(eventLog.GetEventsByType(DecodedEventType.TrainingScenarioUpdated));
    }

    [Fact]
    public async Task SimulationServiceStartsOnlyWhenConfigured()
    {
        var simulation = new FakeSimulationService();
        var service = CreateService(TrainingModeConfiguration.Default with
        {
            TrainingModeEnabled = true,
            UseSimulatedAprsSource = true,
            AutoStartSimulation = true
        }, simulation);

        await service.StartSelectedScenarioAsync();

        Assert.Equal(1, simulation.StartCallCount);
    }

    [Fact]
    public async Task ReplayServiceStartsOnlyWhenConfigured()
    {
        var replay = new FakeReplayService();
        var service = CreateService(TrainingModeConfiguration.Default with
        {
            TrainingModeEnabled = true,
            UseReplaySource = true,
            AutoStartReplay = true
        }, replayService: replay);

        await service.StartSelectedScenarioAsync();

        Assert.Equal(1, replay.StartCallCount);
    }

    [Fact]
    public async Task TrainingModeDoesNotCallTransmit()
    {
        var simulation = new FakeSimulationService();
        var replay = new FakeReplayService();
        var service = CreateService(TrainingModeConfiguration.Default with
        {
            TrainingModeEnabled = true,
            UseSimulatedAprsSource = true,
            UseReplaySource = true,
            AutoStartSimulation = true,
            AutoStartReplay = true
        }, simulation, replay);

        await service.StartSelectedScenarioAsync();

        Assert.Equal(0, simulation.AprsIsTransmitCallCount);
        Assert.Equal(0, simulation.RfTransmitCallCount);
        Assert.Equal(0, simulation.IGateTransmitCallCount);
        Assert.Equal(0, simulation.DigipeaterTransmitCallCount);
        Assert.Equal(0, replay.AprsIsTransmitCallCount);
        Assert.Equal(0, replay.RfTransmitCallCount);
    }

    private static TrainingModeService CreateService(
        TrainingModeConfiguration? configuration = null,
        ISimulationService? simulationService = null,
        IReplayService? replayService = null,
        IDecodedEventLogService? decodedEventLog = null)
    {
        return new TrainingModeService(configuration, simulationService, replayService, decodedEventLog, new FakeClock { UtcNow = Now });
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeSimulationService : ISimulationService
    {
        public int StartCallCount { get; private set; }

        public int AprsIsTransmitCallCount { get; }

        public int RfTransmitCallCount { get; }

        public int IGateTransmitCallCount { get; }

        public int DigipeaterTransmitCallCount { get; }

        public SimulationConfiguration Configuration { get; } = SimulationConfiguration.Default with { SimulationEnabled = true };

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            return Task.CompletedTask;
        }

        public void Stop()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Reset()
        {
        }

        public Task<IReadOnlyList<SimulatedAprsPacket>> GenerateNextBatchAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SimulatedAprsPacket>>([]);
        }

        public SimulationStatus GetStatus()
        {
            return new SimulationStatus(SimulationState.Running, true, true, "Fake", 0, null, null);
        }

        public IReadOnlyList<SimulatedAprsPacket> GetRecentPackets(int? maximumCount = null)
        {
            return [];
        }
    }

    private sealed class FakeReplayService : IReplayService
    {
        public int StartCallCount { get; private set; }

        public int AprsIsTransmitCallCount { get; }

        public int RfTransmitCallCount { get; }

        public ReplaySessionConfiguration Configuration { get; private set; } = ReplaySessionConfiguration.Default;

        public Task<IReadOnlyList<ReplayLogEntry>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ReplayLogEntry>>([]);
        }

        public IReadOnlyList<ReplayLogEntry> LoadEntries(IEnumerable<RawPacketLogEntry> rawLogEntries)
        {
            return [];
        }

        public Task StartReplayAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> PlayNextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Stop()
        {
        }

        public bool SeekToEntryIndex(int entryIndex)
        {
            return false;
        }

        public bool SeekToTimestamp(DateTimeOffset timestampUtc)
        {
            return false;
        }

        public void UpdateConfiguration(ReplaySessionConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ReplaySessionStatus GetStatus()
        {
            return new ReplaySessionStatus(ReplaySessionState.Stopped, 0, 0, 1, false, true, null, null, null, null);
        }

        public IReadOnlyList<ReplayLogEntry> GetEntries()
        {
            return [];
        }
    }
}
