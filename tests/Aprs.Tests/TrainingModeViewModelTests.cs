using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class TrainingModeViewModelTests
{
    [Fact]
    public void TrainingViewModelExposesScenariosStateAndTaskProgress()
    {
        var service = new TrainingModeService(TrainingModeConfiguration.Default with { TrainingModeEnabled = true });
        var viewModel = new TrainingModeViewModel(service);

        Assert.NotEmpty(viewModel.Scenarios);
        Assert.NotEmpty(viewModel.Tasks);
        Assert.Contains("Training transmit disabled", viewModel.TransmitStatusText);
        Assert.Contains("0 /", viewModel.ProgressText);

        viewModel.StartCommand.Execute(null);
        Assert.Equal("Running", viewModel.State);

        viewModel.SelectedTask = viewModel.Tasks.First();
        viewModel.CompleteTaskCommand.Execute(null);
        Assert.Contains("1 /", viewModel.ProgressText);
    }

    [Fact]
    public void ViewModelCanSelectScenarioAndPauseResumeStopReset()
    {
        var service = new TrainingModeService(TrainingModeConfiguration.Default with { TrainingModeEnabled = true });
        var viewModel = new TrainingModeViewModel(service);

        viewModel.SelectedScenario = viewModel.Scenarios.Last();
        Assert.Equal(viewModel.Scenarios.Last().ScenarioName, viewModel.SelectedScenarioName);

        viewModel.StartCommand.Execute(null);
        viewModel.PauseCommand.Execute(null);
        Assert.Equal("Paused", viewModel.State);

        viewModel.ResumeCommand.Execute(null);
        Assert.Equal("Running", viewModel.State);

        viewModel.StopCommand.Execute(null);
        Assert.Equal("Stopped", viewModel.State);

        viewModel.ResetCommand.Execute(null);
        Assert.Equal("Ready", viewModel.State);
    }
}
