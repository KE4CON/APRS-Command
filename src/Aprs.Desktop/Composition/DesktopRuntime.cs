using Microsoft.Extensions.DependencyInjection;
using Aprs.Core;
using Aprs.Mapping;
using Aprs.Services;
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

    private DesktopRuntime(ServiceProvider provider, MainWindowViewModel mainViewModel, LiveDataCoordinator coordinator)
    {
        this.provider = provider;
        MainViewModel = mainViewModel;
        Coordinator = coordinator;
    }

    public static DesktopRuntime Create()
    {
        var services = new ServiceCollection();

        // --- Core + services (real implementations) ---
        // StationDatabase and RawPacketLogService have constructors whose parameters are not
        // registered in the container, so they are created via explicit factories rather than
        // letting the DI container pick a constructor it cannot fully satisfy.
        services.AddSingleton<IAprsParser, AprsParser>();
        services.AddSingleton<IStationDatabase>(_ => new StationDatabase());
        services.AddSingleton<IRawPacketLogService>(
            sp => new RawPacketLogService(sp.GetRequiredService<IAprsParser>()));
        services.AddSingleton<AprsIngestionService>();

        // Persisted settings: single source of truth for configuration that survives restarts.
        services.AddSingleton<IAppSettingsStore>(_ => JsonAppSettingsStore.Default);

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
            FirstRunSetupViewModel.CreateDesignTime());     // TODO: wire to first-run/settings service

        var coordinator = new LiveDataCoordinator(
            provider.GetRequiredService<AprsIngestionService>(),
            provider.GetRequiredService<IStationDatabase>(),
            map,
            rawPacketLog);

        return new DesktopRuntime(provider, mainViewModel, coordinator);
    }

    /// <summary>
    /// Starts the refresh loop and opens a receive-only APRS-IS connection so live stations
    /// appear on launch. Receive-only never transmits. To disable auto-connect, comment out
    /// the ConnectAprsIsReceiveOnly call; to use a real callsign, pass it here.
    /// </summary>
    public void Start()
    {
        Coordinator.Start();

        // Callsign and area filter come from the per-user station profile, not hardcoded.
        var profile = StationProfile.Load();
        Coordinator.ConnectAprsIsReceiveOnly(profile.Callsign, filter: profile.BuildAprsIsFilter());
    }

    public async ValueTask DisposeAsync()
    {
        await Coordinator.DisposeAsync().ConfigureAwait(false);
        await provider.DisposeAsync().ConfigureAwait(false);
    }
}
