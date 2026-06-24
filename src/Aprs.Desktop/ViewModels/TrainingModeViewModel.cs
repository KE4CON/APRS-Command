using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class TrainingModeViewModel : INotifyPropertyChanged
{
    private readonly ITrainingModeService trainingModeService;
    private TrainingScenarioRowViewModel? selectedScenario;
    private TrainingTaskRowViewModel? selectedTask;

    public TrainingModeViewModel(ITrainingModeService trainingModeService)
    {
        this.trainingModeService = trainingModeService;
        Scenarios = new ObservableCollection<TrainingScenarioRowViewModel>();
        Tasks = new ObservableCollection<TrainingTaskRowViewModel>();
        RecentEvents = new ObservableCollection<TrainingEventRowViewModel>();
        EnableCommand = new DesktopCommand(Enable);
        DisableCommand = new DesktopCommand(Disable);
        StartCommand = new DesktopCommand(Start);
        PauseCommand = new DesktopCommand(Pause);
        ResumeCommand = new DesktopCommand(Resume);
        StopCommand = new DesktopCommand(Stop);
        ResetCommand = new DesktopCommand(Reset);
        CompleteTaskCommand = new DesktopCommand(() => SetSelectedTaskStatus(TrainingTaskStatus.Completed));
        SkipTaskCommand = new DesktopCommand(() => SetSelectedTaskStatus(TrainingTaskStatus.Skipped));
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TrainingScenarioRowViewModel> Scenarios { get; }

    public ObservableCollection<TrainingTaskRowViewModel> Tasks { get; }

    public ObservableCollection<TrainingEventRowViewModel> RecentEvents { get; }

    public DesktopCommand EnableCommand { get; }

    public DesktopCommand DisableCommand { get; }

    public DesktopCommand StartCommand { get; }

    public DesktopCommand PauseCommand { get; }

    public DesktopCommand ResumeCommand { get; }

    public DesktopCommand StopCommand { get; }

    public DesktopCommand ResetCommand { get; }

    public DesktopCommand CompleteTaskCommand { get; }

    public DesktopCommand SkipTaskCommand { get; }

    public TrainingScenarioRowViewModel? SelectedScenario
    {
        get => selectedScenario;
        set
        {
            selectedScenario = value;
            if (value is not null)
            {
                trainingModeService.SelectScenario(value.ScenarioId);
                Refresh();
            }

            OnPropertyChanged();
        }
    }

    public TrainingTaskRowViewModel? SelectedTask
    {
        get => selectedTask;
        set
        {
            selectedTask = value;
            OnPropertyChanged();
        }
    }

    public string State { get; private set; } = "Disabled";

    public string SelectedScenarioName { get; private set; } = "-";

    public string ScenarioDescription { get; private set; } = "-";

    public string ProgressText { get; private set; } = "0 / 0 tasks";

    public string TransmitStatusText { get; private set; } = "Training transmit disabled";

    public string LastEventText { get; private set; } = "-";

    public void Refresh()
    {
        var status = trainingModeService.GetStatus();
        State = status.State.ToString();
        SelectedScenarioName = status.SelectedScenario?.ScenarioName ?? "-";
        ScenarioDescription = status.SelectedScenario?.Description ?? "-";
        ProgressText = $"{status.CompletedTaskCount} / {status.TotalTaskCount} tasks ({status.ProgressPercent:0}%)";
        TransmitStatusText = status.TransmitDisabled ? "Training transmit disabled" : "Training transmit enabled";
        LastEventText = status.LastEvent ?? "-";

        Replace(Scenarios, trainingModeService.ListScenarios().Select(scenario => new TrainingScenarioRowViewModel(scenario)));
        Replace(Tasks, trainingModeService.GetCurrentTasks().Select(task => new TrainingTaskRowViewModel(task)));
        Replace(RecentEvents, trainingModeService.GetRecentEvents(25).Select(item => new TrainingEventRowViewModel(item)));

        selectedScenario = Scenarios.FirstOrDefault(row => status.SelectedScenario?.ScenarioId == row.ScenarioId);
        OnPropertyChanged(nameof(SelectedScenario));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(SelectedScenarioName));
        OnPropertyChanged(nameof(ScenarioDescription));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(TransmitStatusText));
        OnPropertyChanged(nameof(LastEventText));
    }

    public static TrainingModeViewModel CreateDesignTime()
    {
        var service = new TrainingModeService(
            TrainingModeConfiguration.Default with { TrainingModeEnabled = true },
            simulationService: new SimulationService(new NoOpSimulatedAprsPacketSink(), SimulationConfiguration.Default with { SimulationEnabled = true }),
            replayService: new ReplayService(new NoOpReplayPacketSink()));
        service.EnableTrainingMode();
        return new TrainingModeViewModel(service);
    }

    private void Enable()
    {
        trainingModeService.EnableTrainingMode();
        Refresh();
    }

    private void Disable()
    {
        trainingModeService.DisableTrainingMode();
        Refresh();
    }

    private void Start()
    {
        trainingModeService.StartSelectedScenarioAsync().GetAwaiter().GetResult();
        Refresh();
    }

    private void Pause()
    {
        trainingModeService.PauseScenario();
        Refresh();
    }

    private void Resume()
    {
        trainingModeService.ResumeScenario();
        Refresh();
    }

    private void Stop()
    {
        trainingModeService.StopScenario();
        Refresh();
    }

    private void Reset()
    {
        trainingModeService.ResetScenario();
        Refresh();
    }

    private void SetSelectedTaskStatus(TrainingTaskStatus status)
    {
        if (selectedTask is null)
        {
            return;
        }

        trainingModeService.SetTaskStatus(selectedTask.TaskId, status);
        Refresh();
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
