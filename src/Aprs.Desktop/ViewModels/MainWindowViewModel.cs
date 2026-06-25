namespace Aprs.Desktop.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private MainFeaturePanel selectedFeature = MainFeaturePanel.Messages;

    public MainWindowViewModel(MapViewModel map)
        : this(
            map,
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
            ConnectionsViewModel.CreateDesignTime())
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
        ConnectionsViewModel connections)
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
        Map.AttachObjectManager(ObjectManager);

        OpenMessagesCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Messages));
        OpenObjectsCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Objects));
        OpenWeatherCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Weather));
        OpenEventsCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Events));
        OpenEventBusCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.EventBus));
        OpenReplayCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Replay));
        OpenRfDiagnosticsCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.RfDiagnostics));
        OpenAlertsCommand = new DesktopCommand(() => SelectFeature(MainFeaturePanel.Alerts));
        OpenHelpCommand = new DesktopCommand(RequestHelp);
        OpenSettingsCommand = new DesktopCommand(RequestSettings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? HelpRequested;

    public event EventHandler? SettingsRequested;

    public MapViewModel Map { get; }

    public StationListViewModel StationList { get; }

    public GpsStatusViewModel GpsStatus { get; }

    public RawPacketLogViewModel RawPacketLog { get; }

    public DecodedEventLogViewModel DecodedEventLog { get; }

    public EventMonitorViewModel EventMonitor { get; }

    public MessageCenterViewModel MessageCenter { get; }

    public ObjectManagerViewModel ObjectManager { get; }

    public DirewolfProfileViewModel DirewolfProfile { get; }

    public ConnectionsViewModel Connections { get; }

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

    public DesktopCommand OpenMessagesCommand { get; }

    public DesktopCommand OpenObjectsCommand { get; }

    public DesktopCommand OpenWeatherCommand { get; }

    public DesktopCommand OpenEventsCommand { get; }

    public DesktopCommand OpenEventBusCommand { get; }

    public DesktopCommand OpenReplayCommand { get; }

    public DesktopCommand OpenRfDiagnosticsCommand { get; }

    public DesktopCommand OpenAlertsCommand { get; }

    public DesktopCommand OpenHelpCommand { get; }

    public DesktopCommand OpenSettingsCommand { get; }

    public MainFeaturePanel SelectedFeature
    {
        get => selectedFeature;
        set
        {
            if (selectedFeature == value)
            {
                return;
            }

            selectedFeature = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedFeatureIndex));
            OnPropertyChanged(nameof(SelectedFeatureName));
            OnPropertyChanged(nameof(SelectedFeatureDescription));
            OnPropertyChanged(nameof(SelectedFeatureContent));
        }
    }

    public int SelectedFeatureIndex
    {
        get => (int)SelectedFeature;
        set
        {
            if (!Enum.IsDefined(typeof(MainFeaturePanel), value))
            {
                return;
            }

            SelectedFeature = (MainFeaturePanel)value;
        }
    }

    public string SelectedFeatureName => SelectedFeature switch
    {
        MainFeaturePanel.EventBus => "Event Bus",
        MainFeaturePanel.RfDiagnostics => "RF Diagnostics",
        _ => SelectedFeature.ToString()
    };

    public string SelectedFeatureDescription => SelectedFeature switch
    {
        MainFeaturePanel.Messages => "Inbox, outbox, bulletins, announcements, and queries.",
        MainFeaturePanel.Objects => "Local and received APRS objects/items with safe edit previews.",
        MainFeaturePanel.Weather => "APRS and local weather station display.",
        MainFeaturePanel.Events => "Decoded packet and application event log.",
        MainFeaturePanel.EventBus => "Internal event bus monitor for troubleshooting.",
        MainFeaturePanel.Replay => "Replay packet files without live transmit.",
        MainFeaturePanel.RfDiagnostics => "RF/TNC, KISS, Direwolf, and AGWPE diagnostics.",
        MainFeaturePanel.Alerts => "Alert rules, trigger history, and geofence-related alerts.",
        MainFeaturePanel.Geofence => "Geofence editor and area alert setup.",
        MainFeaturePanel.Simulation => "Safe simulation controls and generated sample traffic.",
        MainFeaturePanel.Training => "Training scenarios that cannot transmit.",
        MainFeaturePanel.Files => "File import/export hook status and manual export preparation.",
        MainFeaturePanel.Settings => "Station setup, first-run setup, transport, map, and safety settings.",
        _ => "Selected APRS Command feature."
    };

    public object SelectedFeatureContent => SelectedFeature switch
    {
        MainFeaturePanel.Messages => MessageCenter,
        MainFeaturePanel.Objects => ObjectManager,
        MainFeaturePanel.Weather => Weather,
        MainFeaturePanel.Events => DecodedEventLog,
        MainFeaturePanel.EventBus => EventMonitor,
        MainFeaturePanel.Replay => Replay,
        MainFeaturePanel.RfDiagnostics => RfDiagnostics,
        MainFeaturePanel.Alerts => Alerts,
        MainFeaturePanel.Geofence => Geofences,
        MainFeaturePanel.Simulation => Simulation,
        MainFeaturePanel.Training => Training,
        MainFeaturePanel.Files => FileHooks,
        MainFeaturePanel.Settings => FirstRunSetup,
        _ => MessageCenter
    };

    public static MainWindowViewModel CreateDesignTime()
    {
        return new MainWindowViewModel(MapViewModel.CreateDesignTime());
    }

    private void SelectFeature(MainFeaturePanel feature)
    {
        SelectedFeature = feature;
    }

    private void RequestHelp()
    {
        HelpRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RequestSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
