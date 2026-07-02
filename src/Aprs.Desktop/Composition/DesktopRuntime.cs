using Microsoft.Extensions.DependencyInjection;
using Aprs.Core;
using Aprs.Mapping;
using Aprs.Services;
using Aprs.Transport;
using Aprs.Desktop.Services;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Composition;

/// <summary>
/// Real (non-design-time) application composition root. Builds the service container,
/// constructs the live receive spine (map, station list, packet monitor) backed by real
/// services, and assembles the <see cref="MainWindowViewModel"/> used at runtime.
///
/// Spine panels (map, station list, packet monitor) are LIVE. The remaining feature panels
/// are still constructed from CreateDesignTime() sample data and are marked TODO below; wire
/// each to its real service using the same pattern as the spine.
/// </summary>
public sealed class DesktopRuntime : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    /// <summary>Resolves a service from the DI container — used by feature windows that need live services.</summary>
    public T GetService<T>() where T : notnull => provider.GetRequiredService<T>();

    public MainWindowViewModel MainViewModel { get; }
    public LiveDataCoordinator Coordinator { get; }
    public BeaconService BeaconService { get; }
    public ITransmitSafetyAuthority TransmitAuthority { get; }
    public GpsCoordinator GpsCoordinator { get; }
    public MessageAckCoordinator MessageAckCoordinator { get; }
    public KissTcpCoordinator KissTcpCoordinator { get; }
    public SerialKissCoordinator SerialKissCoordinator { get; }
    public ManagedModemCoordinator? ManagedModemCoordinator { get; }
    public NwsAlertService NwsAlertService { get; }
    public AprsIsFailoverCoordinator? FailoverCoordinator { get; }
    public Aprs.Desktop.Services.RadarAnimationService RadarAnimationService { get; }

    public Aprs.Services.PacketStatisticsService PacketStatisticsService { get; }
    public Aprs.Desktop.Services.VoiceAlertService VoiceAlertService { get; }
    public Aprs.Desktop.Services.MobileCompanionServer? MobileCompanionServer { get; private set; }
    public ConnectionHealthWatchdog ConnectionHealthWatchdog { get; }
    public StationTrailService StationTrailService { get; }

    public AprsIsConnectionState ConnectionState => Coordinator.ConnectionState;
    public bool IsTransmitInhibited => TransmitAuthority.IsInhibited;

    private DesktopRuntime(ServiceProvider provider, MainWindowViewModel mainViewModel, LiveDataCoordinator coordinator, BeaconService beaconService, ITransmitSafetyAuthority transmitAuthority, GpsCoordinator gpsCoordinator, MessageAckCoordinator messageAckCoordinator, KissTcpCoordinator kissTcpCoordinator, SerialKissCoordinator serialKissCoordinator, ManagedModemCoordinator? managedModemCoordinator, ConnectionHealthWatchdog connectionHealthWatchdog, StationTrailService stationTrailService, NwsAlertService nwsAlertService, AprsIsFailoverCoordinator? failoverCoordinator, Aprs.Desktop.Services.RadarAnimationService radarAnimationService)
    {
        this.provider = provider;
        MainViewModel = mainViewModel;
        Coordinator = coordinator;
        BeaconService = beaconService;
        TransmitAuthority = transmitAuthority;
        GpsCoordinator = gpsCoordinator;
        MessageAckCoordinator = messageAckCoordinator;
        KissTcpCoordinator = kissTcpCoordinator;
        SerialKissCoordinator = serialKissCoordinator;
        ManagedModemCoordinator = managedModemCoordinator;
        ConnectionHealthWatchdog = connectionHealthWatchdog;
        StationTrailService = stationTrailService;
        NwsAlertService = nwsAlertService;
        FailoverCoordinator = failoverCoordinator;
        RadarAnimationService = radarAnimationService;
        PacketStatisticsService = new Aprs.Services.PacketStatisticsService();
        VoiceAlertService = new Aprs.Desktop.Services.VoiceAlertService();
        ApplyVoiceSettings(JsonAppSettingsStore.Default.Load().Voice);
    }

    public static DesktopRuntime Create()
    {
        var services = new ServiceCollection();

        // --- Core + services (real implementations) ---
        // StationDatabase and RawPacketLogService have constructors whose parameters are not
        // registered in the container, so they are created via explicit factories rather than
        // letting the DI container pick a constructor it cannot fully satisfy.
        services.AddSingleton<IAprsParser, AprsParser>();
        services.AddSingleton<IStationDatabase>(_ => new Persistence.SqliteStationDatabase());
        services.AddSingleton<IRawPacketLogService>(
            sp => new RawPacketLogService(sp.GetRequiredService<IAprsParser>()));
        services.AddSingleton<AprsIngestionService>();

        // Persisted settings: single source of truth for configuration that survives restarts.
        services.AddSingleton<IAppSettingsStore>(_ => JsonAppSettingsStore.Default);

        // One shared port registry, and the central transmit-safety authority that every transmit
        // path consults before keying up (owns the global inhibit used by exercise/training modes).
        services.AddSingleton<IAprsPortManager, AprsPortManager>();
        services.AddSingleton<ITransmitPolicyContext, SettingsTransmitPolicyContext>();
        services.AddSingleton<ITransmitSafetyAuthority, TransmitSafetyAuthority>();
        services.AddSingleton<IAprsMessageStoreService, AprsMessageStoreService>();
        services.AddSingleton<IAlertRuleService, AlertRuleService>();
        services.AddSingleton<IIGateService>(_ => new IGateService(
            new NullAprsIsClient(), IGateConfiguration.Default, null));
        // rfTransmitClient delegates are wired after coordinators are created below.
        var rfTransmitClient = new KissRfBeaconTransmitClient();
        services.AddSingleton<IDigipeaterService>(p => new DigipeaterService(
            p.GetRequiredService<IAprsPortManager>(), rfTransmitClient, null, null, p.GetRequiredService<ITransmitSafetyAuthority>()));
        services.AddSingleton<IRfDiagnosticsService, RfDiagnosticsService>();
        services.AddSingleton<DirewolfProfileService>(_ =>
            new DirewolfProfileService(DateTimeOffset.UtcNow));
        services.AddSingleton<IWeatherDisplayService, WeatherDisplayService>();

        // Offline map download
        services.AddSingleton<Aprs.Mapping.IMapTileCalculationService, Aprs.Mapping.MapTileCalculationService>();
        services.AddSingleton<Aprs.Mapping.IMapTileDownloadClient, Aprs.Mapping.HttpMapTileDownloadClient>();
        services.AddSingleton<Aprs.Mapping.IMapTileCacheService>(_ =>
            new Aprs.Mapping.MapTileCacheService(Aprs.Mapping.MapTileCacheConfiguration.Default));
        services.AddSingleton<Aprs.Mapping.IOfflineMapDownloadManager, Aprs.Mapping.OfflineMapDownloadManager>();
        services.AddSingleton<ViewModels.OfflineMapDownloadViewModel>();
        services.AddSingleton<ViewModels.FrequencyReferenceViewModel>();
        services.AddSingleton<ViewModels.ShadowBeaconViewModel>();
        services.AddSingleton<ViewModels.NetScriptEditorViewModel>();

        // Scheduled message service — created after MessageCenterViewModel is available
        services.AddSingleton<Runtime.ScheduledMessageService>(provider =>
        {
            var msgCenter = provider.GetRequiredService<ViewModels.MessageCenterViewModel>();
            var settings  = provider.GetRequiredService<IAppSettingsStore>().Load();
            var callsign  = settings.Station.Callsign ?? string.Empty;
            return new Runtime.ScheduledMessageService(
                async (recipient, body, _) =>
                    await msgCenter.SendMessageAsync(callsign, recipient, body).ConfigureAwait(false));
        });
        services.AddSingleton<ViewModels.ScheduledMessagesViewModel>();
        services.AddSingleton<ViewModels.MessageReceiptsDashboardViewModel>();

        // Replay service
        services.AddSingleton<IReplayPacketSink, LiveReplayPacketSink>();
        services.AddSingleton<IReplayService>(provider =>
            new ReplayService(
                provider.GetRequiredService<IReplayPacketSink>(),
                ReplaySessionConfiguration.Default));

        // Geofence service
        services.AddSingleton<IGeofenceService, GeofenceService>();

        // Weather beacon scheduler
        services.AddSingleton<ILocalStationProfileService, LocalStationProfileService>();
        services.AddSingleton<IWeatherInputDriverManager, WeatherInputDriverManager>();
        services.AddSingleton<IWeatherObservationSourceProvider, WeatherDriverObservationSourceProvider>();
        services.AddSingleton<IAprsWeatherFormatter, AprsWeatherFormatter>();

        // Object manager and editor
        services.AddSingleton<IAprsObjectManager, AprsObjectManager>();
        services.AddSingleton<IAprsObjectEditorService>(provider =>
            new AprsObjectEditorService(
                provider.GetRequiredService<IAprsObjectManager>(),
                provider.GetRequiredService<ILocalStationProfileService>()));

        var provider = services.BuildServiceProvider();

        // --- Live spine view models ---
        var map = new MapViewModel(Array.Empty<StationMarker>());
        var rawPacketLog = new RawPacketLogViewModel(provider.GetRequiredService<IRawPacketLogService>());

        // MainWindowViewModel's full constructor builds StationList from the map, so the
        // station list is live automatically once the map is updated by the coordinator.
        var mainViewModel = new MainWindowViewModel(
            map,
            GpsStatusViewModel.FromGpsService(new Aprs.Services.GpsService(), DateTimeOffset.UtcNow), // TODO: update from live GpsCoordinator
            rawPacketLog,                                   // LIVE
            DecodedEventLogViewModel.CreateDesignTime(),    // TODO: wire to decoded event log service
            EventMonitorViewModel.CreateDesignTime(),       // TODO: wire to IAprsEventBus
            new MessageCenterViewModel(provider.GetRequiredService<IAprsMessageStoreService>()),  // LIVE
            new ObjectManagerViewModel(
                provider.GetRequiredService<IAprsObjectManager>(),
                provider.GetRequiredService<IAprsObjectEditorService>()),  // LIVE — transmit service wired in Create()
            new DirewolfProfileViewModel(provider.GetRequiredService<DirewolfProfileService>()), // LIVE
            new PortStatusViewModel(provider.GetRequiredService<IAprsPortManager>()),   // LIVE
            new IGateStatusViewModel(provider.GetRequiredService<IIGateService>()),     // LIVE
            new DigipeaterStatusViewModel(provider.GetRequiredService<IDigipeaterService>()), // LIVE
            new WeatherViewModel(provider.GetRequiredService<IWeatherDisplayService>(), DateTimeOffset.UtcNow), // LIVE
            new ReplayViewModel(provider.GetRequiredService<IReplayService>()),  // LIVE
            new RfDiagnosticsViewModel(provider.GetRequiredService<IRfDiagnosticsService>()), // LIVE
            new AlertRulesViewModel(provider.GetRequiredService<IAlertRuleService>()),  // LIVE
            new GeofenceEditorViewModel(provider.GetRequiredService<IGeofenceService>()),  // LIVE
            SimulationViewModel.CreateDesignTime(),         // TODO: wire to simulation service (source=Simulation)
            TrainingModeViewModel.CreateDesignTime(),       // TODO: wire to training service
            FileHooksViewModel.CreateDesignTime(),          // TODO: wire to file hooks service
            FirstRunSetupViewModel.CreateDesignTime(),      // TODO: wire to first-run/settings service
            new ConnectionsViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new StationSetupViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new IGateConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new DigipeaterConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new AudioConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new GpsConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new ManagedModemViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new ReadinessViewModel(), // LIVE — refreshed by WireReadiness() in App.axaml.cs
            new NetControlViewModel(provider.GetRequiredService<IStationDatabase>()), // LIVE
            new NwsAlertsViewModel(), // LIVE — updated by WireNwsAlerts() in App.axaml.cs
            new MessageTemplatesViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new SmartBeaconingViewModel(provider.GetRequiredService<IAppSettingsStore>())); // LIVE

        var coordinator = new LiveDataCoordinator(
            provider.GetRequiredService<AprsIngestionService>(),
            provider.GetRequiredService<IStationDatabase>(),
            map,
            rawPacketLog);

        // Simulation service — routes generated packets through the live ingestion pipeline.
        var simulationSink    = new LiveSimulatedPacketSink(provider.GetRequiredService<AprsIngestionService>());
        var simulationService = new SimulationService(simulationSink, SimulationConfiguration.Default);
        mainViewModel.Simulation.SetSimulationService(simulationService);

        var beaconService = BeaconService.CreateFromSettings(
            provider.GetRequiredService<IAppSettingsStore>().Load(),
            rfBeaconClient: rfTransmitClient);

        // Message ACK coordinator — reuses the beacon service's transmit-capable APRS-IS client.
        var messageStore = provider.GetRequiredService<IAprsMessageStoreService>();
        var messageAckCoordinator = beaconService.AprsIsClient is not null
            ? MessageAckCoordinator.Create(messageStore, beaconService.AprsIsClient, transmitConfirmed: true)
            : MessageAckCoordinator.Create(messageStore, new NullAprsIsClient(), transmitConfirmed: false);

        // Object transmit service — wires the live APRS-IS client into the object manager viewmodel.
        if (beaconService.AprsIsClient is not null)
        {
            var objManager  = provider.GetRequiredService<IAprsObjectManager>();
            var objEditor   = provider.GetRequiredService<IAprsObjectEditorService>();
            var objTransmit = new ObjectTransmitService(objEditor, objManager, beaconService.AprsIsClient);
            mainViewModel.ObjectManager.SetTransmitService(objTransmit);

            // Weather beacon scheduler — reuses the same live APRS-IS client.
            var wxScheduler = new WeatherBeaconScheduler(
                provider.GetRequiredService<ILocalStationProfileService>(),
                provider.GetRequiredService<IAprsWeatherFormatter>(),
                provider.GetRequiredService<IWeatherObservationSourceProvider>(),
                beaconService.AprsIsClient);
            mainViewModel.Weather.SetBeaconScheduler(wxScheduler);

            // Shadow beacon service — reuses the same live APRS-IS client.
            var shadowService = new ShadowBeaconService(
                beaconService.AprsIsClient,
                provider.GetRequiredService<IAppSettingsStore>().Load().Station.Callsign);
            provider.GetRequiredService<ViewModels.ShadowBeaconViewModel>().SetService(shadowService);
        }

        // GPS — only create a serial source when a port is configured and GPS is enabled.
        var gpsSettings = provider.GetRequiredService<IAppSettingsStore>().Load().Gps;
        var gpsdSettings = provider.GetRequiredService<IAppSettingsStore>().Load().Gpsd;

        SerialNmeaGpsSource? gpsSource = null;
        Aprs.Services.GpsdClient? gpsdClient = null;

        if (gpsdSettings.Enabled)
        {
            // GPSD mode — connect to the local GPS daemon.
            var gpsdConfig = new Aprs.Services.GpsdConfiguration(
                Host:            gpsdSettings.Host,
                Port:            gpsdSettings.Port,
                Enabled:         true,
                ReconnectEnabled: true,
                ReconnectDelay:  TimeSpan.FromSeconds(5),
                ReadTimeout:     TimeSpan.FromSeconds(30),
                SourceName:      "gpsd");
            gpsdClient = new Aprs.Services.GpsdClient(gpsdConfig);
        }
        else if (gpsSettings.Enabled && !string.IsNullOrWhiteSpace(gpsSettings.SerialPortName))
        {
            // Serial NMEA mode.
            gpsSource = new SerialNmeaGpsSource(gpsSettings.SerialPortName, gpsSettings.BaudRate);
        }

        var gpsCoordinator = new GpsCoordinator(new Aprs.Services.GpsService(), gpsSource, gpsdClient);

        var appSettings2 = provider.GetRequiredService<IAppSettingsStore>().Load();
        var managedModemCoordinator = ManagedModemCoordinator.CreateIfEnabled(appSettings2, provider.GetRequiredService<AprsIngestionService>());

        var watchdog = new ConnectionHealthWatchdog(coordinator);
        var stationTrailService = new StationTrailService();
        var nwsAlertService = new NwsAlertService();
        var radarAnimationService = new Aprs.Desktop.Services.RadarAnimationService();

        // Build failover coordinator from the configured APRS-IS port.
        AprsIsFailoverCoordinator? failoverCoordinator = null;
        var aprsIsPort = appSettings2.Connections.Ports.FirstOrDefault(p => p.Type == ConnectionPortType.AprsIs);
        if (aprsIsPort?.Configuration.AprsIs is { } aprsIsConfig && aprsIsConfig.FailoverServers?.Count > 0)
        {
            var allServers = aprsIsConfig.AllServers();
            if (allServers.Count > 1)
            {
                failoverCoordinator = new AprsIsFailoverCoordinator(
                    coordinator,
                    allServers,
                    appSettings2.Station.Callsign,
                    aprsIsPort.Configuration.AprsIs.Filter);
            }
        }

        var kissTcpCoordinator = KissTcpCoordinator.CreateFromSettings(
            provider.GetRequiredService<IAppSettingsStore>().Load(),
            provider.GetRequiredService<AprsIngestionService>());

        var serialKissCoordinator = SerialKissCoordinator.CreateFromSettings(
            provider.GetRequiredService<IAppSettingsStore>().Load(),
            provider.GetRequiredService<AprsIngestionService>());

        // Wire real RF transmit delegates now that coordinators exist
        rfTransmitClient.GetTcpClients    = () => kissTcpCoordinator.GetTransmitClients();
        rfTransmitClient.GetSerialClients = () => serialKissCoordinator.GetTransmitClients();

        return new DesktopRuntime(provider, mainViewModel, coordinator, beaconService,
            provider.GetRequiredService<ITransmitSafetyAuthority>(),
            gpsCoordinator,
            messageAckCoordinator,
            kissTcpCoordinator,
            serialKissCoordinator,
            managedModemCoordinator,
            watchdog,
            stationTrailService,
            nwsAlertService,
            failoverCoordinator,
            radarAnimationService);
    }

    /// <summary>
    /// Starts the refresh loop and opens a receive-only APRS-IS connection so live stations
    /// appear on launch. Receive-only never transmits. To disable auto-connect, comment out
    /// the ConnectAprsIsReceiveOnly call; to use a real callsign, pass it here.
    /// </summary>
    public void Start()
    {
        Coordinator.Start();
        BeaconService.Start();
        GpsCoordinator.Start();
        MessageAckCoordinator.Start();
        KissTcpCoordinator.Start();
        SerialKissCoordinator.Start();
        ManagedModemCoordinator?.Start();
        ConnectionHealthWatchdog.Start();
        FailoverCoordinator?.Start();

        // Read the station profile and the first configured APRS-IS port so the receive
        // connection uses the operator's chosen server, port, and filter — not hardcoded defaults.
        var settings = StationProfile.Load();
        var appSettings = JsonAppSettingsStore.Default.Load();

        // Find the first enabled APRS-IS port in the connection list.
        var aprsIsPort = appSettings.Connections.Ports
            .FirstOrDefault(p => p.Type == ConnectionPortType.AprsIs && p.Enabled && p.ReceiveEnabled);

        var serverHost = aprsIsPort?.Configuration.AprsIs?.ServerHost;
        var serverPort = aprsIsPort?.Configuration.AprsIs?.ServerPort;

        // Use the custom filter from the port if set; otherwise fall back to the
        // position-based radius filter built from the station profile.
        var customFilter = aprsIsPort?.Configuration.AprsIs?.Filter?.Trim();
        var positionFilter = settings.BuildAprsIsFilter();
        var filter = !string.IsNullOrWhiteSpace(customFilter) ? customFilter : positionFilter;

        Coordinator.ConnectAprsIsReceiveOnly(
            settings.Callsign,
            serverHost: serverHost,
            serverPort: serverPort,
            filter: filter);
    }

    /// <summary>Starts the mobile companion web server and returns the URL operators open on their phone.</summary>
    public string StartMobileCompanion()
    {
        if (MobileCompanionServer?.IsRunning == true)
            return MobileCompanionServer.Url;

        var msgCenter = MainViewModel.MessageCenter;
        var netCtrl   = MainViewModel.NetControl;

        MobileCompanionServer = new Aprs.Desktop.Services.MobileCompanionServer(
            services:         provider,
            getCallsign:      () => Configuration.StationProfile.Load().Callsign,
            getConnected:     () => ConnectionState == Aprs.Transport.AprsIsConnectionState.Connected,
            getTopStations:   () => PacketStatisticsService.GetTopStations(10),
            getTotalPackets:  () => PacketStatisticsService.TotalPackets,
            getPacketsPerHour:() => PacketStatisticsService.PacketsPerHour,
            getMessages: () =>
            {
                var list = new List<Aprs.Desktop.Services.MessageSummary>();
                foreach (var m in msgCenter.Inbox.TakeLast(25))
                    list.Add(new(Direction:"in", Other:m.RemoteStation, Body:m.Body,
                        Time: m.Timestamp));
                return list.OrderByDescending(m => m.Time).Take(40).ToList();
            },
            getNetRoster: () =>
                netCtrl.Roster.Select(r => new Aprs.Desktop.Services.NetRosterEntry(
                    Callsign:       r.Callsign,
                    CheckInStatus:  r.Status.ToString(),
                    ResourceStatus: r.ResourceStatus.ToString(),
                    StatusEmoji:    r.Status.ToString() switch
                    {
                        "CheckedIn" => "✅",
                        "Standby"   => "⏸",
                        "Departed"  => "🚪",
                        _           => "📍"
                    })).ToList());

        MobileCompanionServer.Start();
        return MobileCompanionServer.Url;
    }

    public async Task StopMobileCompanionAsync()
    {
        if (MobileCompanionServer is not null)
        {
            await MobileCompanionServer.DisposeAsync().ConfigureAwait(false);
            MobileCompanionServer = null;
        }
    }

    public void ApplyVoiceSettings(Configuration.VoiceSettings v)
    {
        VoiceAlertService.IsEnabled                = v.Enabled;
        VoiceAlertService.SpeakIncomingMessages    = v.SpeakIncomingMessages;
        VoiceAlertService.SpeakNetCheckIns         = v.SpeakNetCheckIns;
        VoiceAlertService.SpeakWeatherAlerts       = v.SpeakWeatherAlerts;
        VoiceAlertService.SpeakStationAlerts       = v.SpeakStationAlerts;
        VoiceAlertService.SpeakConnectionEvents    = v.SpeakConnectionEvents;
        VoiceAlertService.SpeakBeaconConfirmations = v.SpeakBeaconConfirmations;
        VoiceAlertService.PreferredVoiceName       = v.PreferredVoiceName;
    }

    public async ValueTask DisposeAsync()
    {
        VoiceAlertService.Dispose();
        if (MobileCompanionServer is not null)
            await MobileCompanionServer.DisposeAsync().ConfigureAwait(false);
        await RadarAnimationService.DisposeAsync().ConfigureAwait(false);
        if (FailoverCoordinator is not null) await FailoverCoordinator.DisposeAsync().ConfigureAwait(false);
        await NwsAlertService.DisposeAsync().ConfigureAwait(false);
        await ConnectionHealthWatchdog.DisposeAsync().ConfigureAwait(false);
        if (ManagedModemCoordinator is not null) await ManagedModemCoordinator.DisposeAsync().ConfigureAwait(false);
        await SerialKissCoordinator.DisposeAsync().ConfigureAwait(false);
        await KissTcpCoordinator.DisposeAsync().ConfigureAwait(false);
        await MessageAckCoordinator.DisposeAsync().ConfigureAwait(false);
        await GpsCoordinator.DisposeAsync().ConfigureAwait(false);
        await BeaconService.DisposeAsync().ConfigureAwait(false);
        await Coordinator.DisposeAsync().ConfigureAwait(false);

        // Dispose the station database (closes the SQLite connection cleanly).
        if (provider.GetService<IStationDatabase>() is IDisposable db)
        {
            db.Dispose();
        }

        await provider.DisposeAsync().ConfigureAwait(false);
    }
}
