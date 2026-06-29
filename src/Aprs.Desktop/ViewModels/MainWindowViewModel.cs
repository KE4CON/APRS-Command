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
            DigipeaterConfigViewModel.CreateDesignTime(),
            AudioConfigViewModel.CreateDesignTime(),
            GpsConfigViewModel.CreateDesignTime(),
            ManagedModemViewModel.CreateDesignTime(),
            ReadinessViewModel.CreateDesignTime(),
            NetControlViewModel.CreateDesignTime(),
            NwsAlertsViewModel.CreateDesignTime(),
            MessageTemplatesViewModel.CreateDesignTime(),
            SmartBeaconingViewModel.CreateDesignTime())
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
        DigipeaterConfigViewModel digipeaterConfig,
        AudioConfigViewModel audioConfig,
        GpsConfigViewModel gpsConfig,
        ManagedModemViewModel managedModem,
        ReadinessViewModel readiness,
        NetControlViewModel netControl,
        NwsAlertsViewModel nwsAlerts,
        MessageTemplatesViewModel messageTemplates,
        SmartBeaconingViewModel smartBeaconing)
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
        AudioConfig = audioConfig;
        GpsConfig = gpsConfig;
        ManagedModem = managedModem;
        Readiness = readiness;
        NetControl = netControl;
        NwsAlerts = nwsAlerts;
        MessageTemplates = messageTemplates;
        SmartBeaconing = smartBeaconing;
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
        BeaconNowCommand       = new DesktopCommand(() => BeaconNowRequested?.Invoke(this, EventArgs.Empty));
        ToggleExerciseModeCommand = new DesktopCommand(() => ExerciseModeRequested?.Invoke(this, EventArgs.Empty));
        OpenAboutCommand       = new DesktopCommand(() => AboutRequested?.Invoke(this, EventArgs.Empty));
        OpenNetControlCommand  = new DesktopCommand(() => NetControlRequested?.Invoke(this, EventArgs.Empty));
        OpenNwsAlertsCommand   = new DesktopCommand(() => NwsAlertsRequested?.Invoke(this, EventArgs.Empty));
        OpenAfterActionCommand     = new DesktopCommand(() => AfterActionRequested?.Invoke(this, EventArgs.Empty));
        OpenOfflineMapCommand      = new DesktopCommand(() => OfflineMapRequested?.Invoke(this, EventArgs.Empty));
        OpenFrequencyRefCommand    = new DesktopCommand(() => FrequencyRefRequested?.Invoke(this, EventArgs.Empty));
        OpenElevationCommand       = new DesktopCommand(() => ElevationRequested?.Invoke(this, EventArgs.Empty));
        OpenShadowBeaconCommand    = new DesktopCommand(() => ShadowBeaconRequested?.Invoke(this, EventArgs.Empty));
        ToggleDarkModeCommand  = new DesktopCommand(() => DarkModeRequested?.Invoke(this, EventArgs.Empty));
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
    public event EventHandler? BeaconNowRequested;
    public event EventHandler? ExerciseModeRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? DarkModeRequested;
    public event EventHandler? NetControlRequested;
    public event EventHandler? NwsAlertsRequested;
    public event EventHandler? AfterActionRequested;
    public event EventHandler? OfflineMapRequested;
    public event EventHandler? FrequencyRefRequested;
    public event EventHandler? ElevationRequested;
    public event EventHandler? ShadowBeaconRequested;

    public MapViewModel Map { get; }
    public StationListViewModel StationList { get; }
    public GpsStatusViewModel GpsStatus { get; private set; }

    /// <summary>Replaces the GPS status viewmodel with fresh data from the live GPS service.</summary>
    public void UpdateGpsStatus(GpsStatusViewModel updated)
    {
        GpsStatus = updated;
        OnPropertyChanged(nameof(GpsStatus));
    }
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
    public AudioConfigViewModel AudioConfig { get; }
    public GpsConfigViewModel GpsConfig { get; }
    public ManagedModemViewModel ManagedModem { get; }
    public ReadinessViewModel Readiness { get; }
    public NetControlViewModel NetControl { get; }
    public NwsAlertsViewModel NwsAlerts { get; }
    public MessageTemplatesViewModel MessageTemplates { get; }
    public SmartBeaconingViewModel SmartBeaconing { get; }

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
    public DesktopCommand BeaconNowCommand { get; }
    public DesktopCommand ToggleExerciseModeCommand { get; }
    public DesktopCommand OpenAboutCommand { get; }
    public DesktopCommand ToggleDarkModeCommand { get; }
    public DesktopCommand OpenNetControlCommand { get; }
    public DesktopCommand OpenNwsAlertsCommand { get; }
    public DesktopCommand OpenAfterActionCommand { get; }
    public DesktopCommand OpenOfflineMapCommand { get; }
    public DesktopCommand OpenFrequencyRefCommand { get; }
    public DesktopCommand OpenElevationCommand { get; }
    public DesktopCommand OpenShadowBeaconCommand { get; }

    public static MainWindowViewModel CreateDesignTime()
    {
        return new MainWindowViewModel(MapViewModel.CreateDesignTime());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
