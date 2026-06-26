using Microsoft.Extensions.DependencyInjection;
using Aprs.Core;
using Aprs.Mapping;
using Aprs.Services;
using Aprs.Transport;
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

    public MainWindowViewModel MainViewModel { get; }
    public LiveDataCoordinator Coordinator { get; }
    public BeaconService BeaconService { get; }
    public ITransmitSafetyAuthority TransmitAuthority { get; }
    public GpsCoordinator GpsCoordinator { get; }

    public AprsIsConnectionState ConnectionState => Coordinator.ConnectionState;
    public bool IsTransmitInhibited => TransmitAuthority.IsInhibited;

    private DesktopRuntime(ServiceProvider provider, MainWindowViewModel mainViewModel, LiveDataCoordinator coordinator, BeaconService beaconService, ITransmitSafetyAuthority transmitAuthority, GpsCoordinator gpsCoordinator)
    {
        this.provider = provider;
        MainViewModel = mainViewModel;
        Coordinator = coordinator;
        BeaconService = beaconService;
        TransmitAuthority = transmitAuthority;
        GpsCoordinator = gpsCoordinator;
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

        var provider = services.BuildServiceProvider();

        // --- Live spine view models ---
        var map = new MapViewModel(Array.Empty<StationMarker>());
        var rawPacketLog = new RawPacketLogViewModel(provider.GetRequiredService<IRawPacketLogService>());

        // MainWindowViewModel's full constructor builds StationList from the map, so the
        // station list is live automatically once the map is updated by the coordinator.
        var mainViewModel = new MainWindowViewModel(
            map,
            GpsStatusViewModel.CreateDesignTime(),          // TODO: wire to real GPS service
            rawPacketLog,                                   // LIVE
            DecodedEventLogViewModel.CreateDesignTime(),    // TODO: wire to decoded event log service
            EventMonitorViewModel.CreateDesignTime(),       // TODO: wire to IAprsEventBus
            MessageCenterViewModel.CreateDesignTime(),      // TODO: wire to messaging services
            ObjectManagerViewModel.CreateDesignTime(),      // TODO: wire to object manager service
            DirewolfProfileViewModel.CreateDesignTime(),    // TODO: wire to Direwolf profile service
            PortStatusViewModel.CreateDesignTime(),         // TODO: wire to AprsPortManager
            IGateStatusViewModel.CreateDesignTime(),        // TODO: wire to iGate service
            DigipeaterStatusViewModel.CreateDesignTime(),   // TODO: wire to digipeater service
            WeatherViewModel.CreateDesignTime(),            // TODO: wire to weather services
            ReplayViewModel.CreateDesignTime(),             // TODO: wire to replay service (feed ingestion, source=Replay)
            RfDiagnosticsViewModel.CreateDesignTime(),      // TODO: wire to RF diagnostics
            AlertRulesViewModel.CreateDesignTime(),         // TODO: wire to alert rules service
            GeofenceEditorViewModel.CreateDesignTime(),     // TODO: wire to geofence service
            SimulationViewModel.CreateDesignTime(),         // TODO: wire to simulation service (source=Simulation)
            TrainingModeViewModel.CreateDesignTime(),       // TODO: wire to training service
            FileHooksViewModel.CreateDesignTime(),          // TODO: wire to file hooks service
            FirstRunSetupViewModel.CreateDesignTime(),      // TODO: wire to first-run/settings service
            new ConnectionsViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new StationSetupViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new IGateConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new DigipeaterConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new AudioConfigViewModel(provider.GetRequiredService<IAppSettingsStore>()), // LIVE
            new GpsConfigViewModel(provider.GetRequiredService<IAppSettingsStore>())); // LIVE

        var coordinator = new LiveDataCoordinator(
            provider.GetRequiredService<AprsIngestionService>(),
            provider.GetRequiredService<IStationDatabase>(),
            map,
            rawPacketLog);

        var beaconService = BeaconService.CreateFromSettings(
            provider.GetRequiredService<IAppSettingsStore>().Load());

        // GPS — only create a serial source when a port is configured and GPS is enabled.
        var gpsSettings = provider.GetRequiredService<IAppSettingsStore>().Load().Gps;
        SerialNmeaGpsSource? gpsSource = null;
        if (gpsSettings.Enabled && !string.IsNullOrWhiteSpace(gpsSettings.SerialPortName))
        {
            gpsSource = new SerialNmeaGpsSource(gpsSettings.SerialPortName, gpsSettings.BaudRate);
        }
        var gpsCoordinator = new GpsCoordinator(new Aprs.Services.GpsService(), gpsSource);

        return new DesktopRuntime(provider, mainViewModel, coordinator, beaconService,
            provider.GetRequiredService<ITransmitSafetyAuthority>(),
            gpsCoordinator);
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

    public async ValueTask DisposeAsync()
    {
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
