using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class SimulationViewModel : INotifyPropertyChanged
{
    private readonly ISimulationService simulationService;

    public SimulationViewModel(ISimulationService simulationService)
    {
        this.simulationService = simulationService;
        RecentPackets = new ObservableCollection<SimulatedPacketRowViewModel>();
        StartCommand = new DesktopCommand(Start);
        PauseCommand = new DesktopCommand(Pause);
        ResumeCommand = new DesktopCommand(Resume);
        StopCommand = new DesktopCommand(Stop);
        StepCommand = new DesktopCommand(Step);
        ResetCommand = new DesktopCommand(Reset);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SimulatedPacketRowViewModel> RecentPackets { get; }

    public DesktopCommand StartCommand { get; }

    public DesktopCommand PauseCommand { get; }

    public DesktopCommand ResumeCommand { get; }

    public DesktopCommand StopCommand { get; }

    public DesktopCommand StepCommand { get; }

    public DesktopCommand ResetCommand { get; }

    public string State { get; private set; } = "Stopped";

    public string SourceName { get; private set; } = "APRS Simulation";

    public string PacketCountText { get; private set; } = "0 packets";

    public string StationCountText { get; private set; } = "0 fixed / 0 mobile / 0 weather";

    public string UpdateIntervalText { get; private set; } = "30 sec";

    public string AreaText { get; private set; } = "-";

    public string TransmitStatusText { get; private set; } = "Simulation transmit disabled";

    public string LastErrorText { get; private set; } = string.Empty;

    public void Refresh()
    {
        var status = simulationService.GetStatus();
        var configuration = simulationService.Configuration;
        State = status.State.ToString();
        SourceName = status.SimulationSourceName;
        PacketCountText = $"{status.GeneratedPacketCount} packets";
        StationCountText = $"{configuration.FixedStationCount} fixed / {configuration.MobileStationCount} mobile / {configuration.WeatherStationCount} weather";
        UpdateIntervalText = $"{configuration.UpdateInterval.TotalSeconds:0} sec";
        AreaText = $"{configuration.AreaCenterLatitude:0.00000}, {configuration.AreaCenterLongitude:0.00000} radius {configuration.AreaRadiusMeters:0} m";
        TransmitStatusText = status.TransmitDisabled ? "Simulation transmit disabled" : "Simulation transmit enabled";
        LastErrorText = status.LastError ?? string.Empty;

        RecentPackets.Clear();
        foreach (var row in simulationService.GetRecentPackets(25).Select(packet => new SimulatedPacketRowViewModel(packet)))
        {
            RecentPackets.Add(row);
        }

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(PacketCountText));
        OnPropertyChanged(nameof(StationCountText));
        OnPropertyChanged(nameof(UpdateIntervalText));
        OnPropertyChanged(nameof(AreaText));
        OnPropertyChanged(nameof(TransmitStatusText));
        OnPropertyChanged(nameof(LastErrorText));
    }

    public static SimulationViewModel CreateDesignTime()
    {
        var service = new SimulationService(
            new NoOpSimulatedAprsPacketSink(),
            SimulationConfiguration.Default with
            {
                SimulationEnabled = true,
                FixedStationCount = 2,
                MobileStationCount = 2,
                WeatherStationCount = 1,
                ObjectCount = 1
            });
        service.StartAsync().GetAwaiter().GetResult();
        return new SimulationViewModel(service);
    }

    private void Start()
    {
        simulationService.StartAsync().GetAwaiter().GetResult();
        Refresh();
    }

    private void Pause()
    {
        simulationService.Pause();
        Refresh();
    }

    private void Resume()
    {
        simulationService.Resume();
        Refresh();
    }

    private void Stop()
    {
        simulationService.Stop();
        Refresh();
    }

    private void Step()
    {
        simulationService.GenerateNextBatchAsync().GetAwaiter().GetResult();
        Refresh();
    }

    private void Reset()
    {
        simulationService.Reset();
        Refresh();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
