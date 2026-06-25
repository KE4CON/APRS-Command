using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel(MapViewModel map)
        : this(map,
            GpsStatusViewModel.CreateDesignTime(),
            RawPacketLogViewModel.CreateDesignTime(),
            DecodedEventLogViewModel.CreateDesignTime(),
            EventMonitorViewModel.CreateDesignTime(),
            MessageCenterViewModel.CreateDesignTime(),
            ObjectManagerViewModel.CreateDesignTime(),
            DirewolfProfileViewModel.CreateDesignTime(),
            PortStatusViewModel.CreateDesignTime(),
            IGateStatusViewModel.CreateDesignTime(),
            DigipeaterStatusViewModel.CreateDesignTime(),
            WeatherViewModel.CreateDesignTime(),
            ReplayViewModel.CreateDesignTime(),
            RfDiagnosticsViewModel.CreateDesignTime(),
            AlertRulesViewModel.CreateDesignTime(),
            GeofenceEditorViewModel.CreateDesignTime(),
            SimulationViewModel.CreateDesignTime(),
            TrainingModeViewModel.CreateDesignTime(),
            FileHooksViewModel.CreateDesignTime(),
            FirstRunSetupViewModel.CreateDesignTime(),
            ConnectionsViewModel.CreateDesignTime(),
            StationSetupViewModel.CreateDesignTime(),
            IGateConfigViewModel.CreateDesignTime(),
            DigipeaterConfigViewModel.CreateDesignTime())
    {
    }

    public MainWindowViewModel(
        MapViewModel map,
        GpsStatusViewModel gpsStatus,
        RawPacketLogViewModel rawPacketLog,
        DecodedEventLogViewModel decodedEventLog,
        EventMonitorViewModel eventMonitor,
        MessageCenterViewModel messageCenter,
        ObjectManagerViewModel objectManager,
        DirewolfProfileViewModel direwolfProfile,
        PortStatusViewModel portStatus,
        IGateStatusViewModel iGateStatus,
        DigipeaterStatusViewModel digipeaterStatus,
        WeatherViewModel weather,
        ReplayViewModel replay,
        RfDiagnosticsViewModel rfDiagnostics,
        AlertRulesViewModel alerts,
        GeofenceEditorViewModel geofences,
        SimulationViewModel simulation,
        TrainingModeViewModel training,
        FileHooksViewModel fileHooks,
        FirstRunSetupViewModel firstRunSetup,
        ConnectionsViewModel connections,
        StationSetupViewModel stationSetup,
        IGateConfigViewModel iGateConfig,
        DigipeaterConfigViewModel digipeaterConfig)
    {
        Map = map;
        StationList = new StationListViewModel(map);
        GpsStatus = gpsStatus;
        RawPacketLog = rawPacketLog;
        DecodedEventLog = decodedEventLog;
        EventMonitor = eventMonitor;
        MessageCenter = messageCenter;
        ObjectManager = objectManager;
        DirewolfProfile = direwolfProfile;
        PortStatus = portStatus;
        IGateStatus = iGateStatus;
        DigipeaterStatus = digipeaterStatus;
        Weather = weather;
        Replay = replay;
        RfDiagnostics = rfDiagnostics;
        Alerts = alerts;
        Geofences = geofences;
        Simulation = simulation;
        Training = training;
        FileHooks = fileHooks;
        FirstRunSetup = firstRunSetup;
        Connections = connections;
        StationSetup = stationSetup;
        IGateConfig = iGateConfig;
        DigipeaterConfig = digipeaterConfig;
        Map.AttachObjectManager(ObjectManager);

        // All feature panels now open as their own windows.
        // Each command raises an event; MainWindow.axaml.cs handles the event and opens the window.
        OpenMessagesCommand    = new DesktopCommand(() => MessagesRequested?.Invoke(this, EventArgs.Empty));
        OpenObjectsCommand     = new DesktopCommand(() => ObjectsRequested?.Invoke(this, EventArgs.Empty));
        OpenWeatherCommand     = new DesktopCommand(() => WeatherRequested?.Invoke(this, EventArgs.Empty));
        OpenEventsCommand      = new DesktopCommand(() => EventsRequested?.Invoke(this, EventArgs.Empty));
        OpenEventBusCommand    = new DesktopCommand(() => EventBusRequested?.Invoke(this, EventArgs.Empty));
        OpenReplayCommand      = new DesktopCommand(() => ReplayRequested?.Invoke(this, EventArgs.Empty));
        OpenRfDiagnosticsCommand = new DesktopCommand(() => RfDiagnosticsRequested?.Invoke(this, EventArgs.Empty));
        OpenAlertsCommand      = new DesktopCommand(() => AlertsRequested?.Invoke(this, EventArgs.Empty));
        OpenStationListCommand = new DesktopCommand(() => StationListRequested?.Invoke(this, EventArgs.Empty));
        OpenRawPacketsCommand  = new DesktopCommand(() => RawPacketsRequested?.Invoke(this, EventArgs.Empty));
        OpenSettingsCommand    = new DesktopCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        OpenHelpCommand        = new DesktopCommand(() => HelpRequested?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Window-open events — MainWindow subscribes and opens the appropriate window.
    public event EventHandler? MessagesRequested;
    public event EventHandler? ObjectsRequested;
    public event EventHandler? WeatherRequested;
    public event EventHandler? EventsRequested;
    public event EventHandler? EventBusRequested;
    public event EventHandler? ReplayRequested;
    public event EventHandler? RfDiagnosticsRequested;
    public event EventHandler? AlertsRequested;
    public event EventHandler? StationListRequested;
    public event EventHandler? RawPacketsRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? HelpRequested;

    public MapViewModel Map { get; }
    public StationListViewModel StationList { get; }
    public GpsStatusViewModel GpsStatus { get; }
    public RawPacketLogViewModel RawPacketLog { get; }
    public DecodedEventLogViewModel DecodedEventLog { get; }
    public EventMonitorViewModel EventMonitor { get; }
    public MessageCenterViewModel MessageCenter { get; }
    public ObjectManagerViewModel ObjectManager { get; }
    public DirewolfProfileViewModel DirewolfProfile { get; }
    public PortStatusViewModel PortStatus { get; }
    public IGateStatusViewModel IGateStatus { get; }
    public DigipeaterStatusViewModel DigipeaterStatus { get; }
    public WeatherViewModel Weather { get; }
    public ReplayViewModel Replay { get; }
    public RfDiagnosticsViewModel RfDiagnostics { get; }
    public AlertRulesViewModel Alerts { get; }
    public GeofenceEditorViewModel Geofences { get; }
    public SimulationViewModel Simulation { get; }
    public TrainingModeViewModel Training { get; }
    public FileHooksViewModel FileHooks { get; }
    public FirstRunSetupViewModel FirstRunSetup { get; }
    public ConnectionsViewModel Connections { get; }
    public StationSetupViewModel StationSetup { get; }
    public IGateConfigViewModel IGateConfig { get; }
    public DigipeaterConfigViewModel DigipeaterConfig { get; }

    public DesktopCommand OpenMessagesCommand { get; }
    public DesktopCommand OpenObjectsCommand { get; }
    public DesktopCommand OpenWeatherCommand { get; }
    public DesktopCommand OpenEventsCommand { get; }
    public DesktopCommand OpenEventBusCommand { get; }
    public DesktopCommand OpenReplayCommand { get; }
    public DesktopCommand OpenRfDiagnosticsCommand { get; }
    public DesktopCommand OpenAlertsCommand { get; }
    public DesktopCommand OpenStationListCommand { get; }
    public DesktopCommand OpenRawPacketsCommand { get; }
    public DesktopCommand OpenSettingsCommand { get; }
    public DesktopCommand OpenHelpCommand { get; }

    public static MainWindowViewModel CreateDesignTime()
    {
        return new MainWindowViewModel(MapViewModel.CreateDesignTime());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
