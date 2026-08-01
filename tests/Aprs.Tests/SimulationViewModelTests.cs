using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class SimulationViewModelTests
{
    [Fact]
    public async Task SimulationViewModelExposesStateAndControls()
    {
        var service = new SimulationService(
            new NoOpSimulatedAprsPacketSink(),
            SimulationConfiguration.Default with
            {
                SimulationEnabled = true,
                FixedStationCount = 1,
                MobileStationCount = 1,
                WeatherStationCount = 1
            });
        var viewModel = new SimulationViewModel(service);

        await viewModel.StartAsync();

        Assert.Equal("Running", viewModel.State);
        Assert.Contains("Simulation transmit disabled", viewModel.TransmitStatusText);
        Assert.NotEmpty(viewModel.RecentPackets);

        viewModel.PauseCommand.Execute(null);
        Assert.Equal("Paused", viewModel.State);

        viewModel.ResumeCommand.Execute(null);
        Assert.Equal("Running", viewModel.State);

        viewModel.StopCommand.Execute(null);
        Assert.Equal("Stopped", viewModel.State);
    }

    [Fact]
    public async Task StepCommandGeneratesAdditionalPacketsWhenRunning()
    {
        var service = new SimulationService(
            new NoOpSimulatedAprsPacketSink(),
            SimulationConfiguration.Default with { SimulationEnabled = true, FixedStationCount = 1, MobileStationCount = 0, WeatherStationCount = 0, ObjectCount = 0, GenerateMessages = false, GenerateBulletins = false });
        var viewModel = new SimulationViewModel(service);

        await viewModel.StartAsync();
        var count = viewModel.RecentPackets.Count;
        await viewModel.StepAsync();

        Assert.True(viewModel.RecentPackets.Count > count);
    }
}
