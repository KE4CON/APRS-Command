using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aprs.Desktop.Composition;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Audio;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Aprs.Desktop.Views;
using Avalonia.Threading;

namespace Aprs.Desktop;

public sealed partial class App : Application
{
    private static void WireMessageToast(DesktopRuntime rt)
    {
        var lastInboxCount = rt.MainViewModel.MessageCenter.InboxCount;

        rt.Coordinator.PacketIngested += (_, _) =>
        {
            var count = rt.MainViewModel.MessageCenter.InboxCount;
            if (count <= lastInboxCount) return;

            var added = count - lastInboxCount;
            lastInboxCount = count;

            Dispatcher.UIThread.Post(() =>
            {
                var latest = rt.MainViewModel.MessageCenter.Inbox.LastOrDefault();
                var from   = latest?.RemoteStation ?? "Unknown";
                var body   = latest?.Body ?? string.Empty;
                var title  = added == 1
                    ? $"New message from {from}"
                    : $"{added} new messages";

                var toast = new ToastNotification(title, body);
                toast.Clicked += (_, _) =>
                    rt.MainViewModel.OpenMessagesCommand.Execute(null);
                toast.Show();
            });
        };
    }

    private static void WireGpsWriteback(DesktopRuntime rt)
    {
        rt.GpsCoordinator.PositionUpdated += (_, position) =>
        {
            // Only write back if the operator has enabled it and we have valid coordinates.
            if (!JsonAppSettingsStore.Default.Load().Gps.UpdateStationPosition) return;
            if (position.Latitude is null || position.Longitude is null) return;
            if (!position.FixValid) return;

            // Update the persisted station profile lat/lon from the live GPS fix.
            JsonAppSettingsStore.Default.Update(s => s with
            {
                Station = s.Station with
                {
                    Latitude  = position.Latitude.Value,
                    Longitude = position.Longitude.Value
                }
            });

            // Push the updated position into the live beacon engine immediately.
            rt.BeaconService.ApplySettings(JsonAppSettingsStore.Default.Load());
        };
    }

    private static void WireMessageAck(DesktopRuntime rt)
    {
        // Give the message center viewmodel access to the ACK coordinator so
        // SendMessageAsync uses the real retry engine.
        rt.MainViewModel.MessageCenter.SetAckCoordinator(rt.MessageAckCoordinator);

        // Route every parsed packet to the ACK coordinator so ACK/REJ packets
        // from APRS-IS are applied to outgoing messages immediately.
        rt.Coordinator.PacketParsed += (_, e) =>
        {
            if (e.Packet is not null)
                rt.MessageAckCoordinator.ProcessIncomingPacket(e.Packet);
        };
    }

    private static void WireRfDiagnostics(DesktopRuntime rt)
    {
        var rfVm = rt.MainViewModel.RfDiagnostics;

        // Route parsed RF packets (KISS-TCP and Direwolf) to the RF diagnostics service.
        rt.Coordinator.PacketParsed += (_, e) =>
        {
            if (e.Packet is null) return;
            if (e.Source is AprsPacketSource.TcpKiss or AprsPacketSource.Direwolf)
            {
                rt.MainViewModel.RfDiagnostics.AcceptPacket(e.Packet, e.Source);
            }
        };

        // Pipe Direwolf / managed modem console output to the RF Diagnostics console.
        if (rt.ManagedModemCoordinator is not null)
        {
            rt.ManagedModemCoordinator.OutputReceived += (_, line) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    rfVm.AddConsoleOutput(line));
            };
        }
    }

    private static void WireGpsStatus(DesktopRuntime rt)
    {
        // Update the GPS status viewmodel whenever a new GPS position arrives.
        rt.GpsCoordinator.PositionUpdated += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var updated = GpsStatusViewModel.FromGpsService(
                    rt.GpsCoordinator.GpsService, DateTimeOffset.UtcNow);
                rt.MainViewModel.UpdateGpsStatus(updated);
            });
        };
    }

    private static void WireConnectionWatchdog(DesktopRuntime rt)
    {
        rt.ConnectionHealthWatchdog.ConnectionLost += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var toast = new Aprs.Desktop.Views.ToastNotification(
                    "⚠ APRS-IS Disconnected",
                    "Connection has been down for 3+ minutes. Check your network and server settings.");
                toast.Show();
            });
        };

        rt.ConnectionHealthWatchdog.ConnectionRestored += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var toast = new Aprs.Desktop.Views.ToastNotification(
                    "✓ APRS-IS Reconnected",
                    "Connection restored.");
                toast.Show();
            });
        };
    }

    private static void WireTrails(DesktopRuntime rt, MainWindow mainWindow)
    {
        var mapView = mainWindow.TheMapView;
        if (mapView is null) return;

        // Give MapView a reference so it can redraw when the toggle fires.
        mapView.WireTrailService(rt.StationTrailService);

        // Record positions from ingested position packets only.
        rt.Coordinator.PacketParsed += (_, e) =>
        {
            if (e.Packet is not Aprs.Core.PositionAprsPacket pos) return;
            if (pos.Latitude is null || pos.Longitude is null) return;
            rt.StationTrailService.RecordPosition(
                pos.SourceCallsign, pos.Latitude.Value, pos.Longitude.Value, pos.ReceivedAtUtc);
        };

        // Redraw trail layer whenever trail data changes.
        rt.StationTrailService.TrailsUpdated += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                mapView.UpdateTrails(rt.StationTrailService));
        };
    }

    private static void WireNwsAlerts(DesktopRuntime rt)
    {
        var settings = JsonAppSettingsStore.Default.Load();
        var lat = settings.Station.Latitude;
        var lon = settings.Station.Longitude;

        // Subscribe to alert updates — push to viewmodel on UI thread.
        rt.NwsAlertService.AlertsRefreshed += (_, alerts) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                rt.MainViewModel.NwsAlerts.UpdateAlerts(alerts));
        };

        // Start polling with the operator's configured location.
        rt.NwsAlertService.Start(lat, lon);
    }

    private static void WireNetControl(DesktopRuntime rt)
    {
        // Update roster when position packets arrive.
        rt.Coordinator.PacketParsed += (_, e) =>
        {
            if (e.Packet is not Aprs.Core.PositionAprsPacket pos) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                rt.MainViewModel.NetControl.ProcessHeardStation(
                    pos.SourceCallsign,
                    pos.Latitude,
                    pos.Longitude,
                    pos.Comment,
                    pos.ReceivedAtUtc));
        };
    }

    private static void WireWeather(DesktopRuntime rt)
    {
        // Route weather packets from the ingestion pipeline to the Weather window.
        rt.Coordinator.PacketParsed += (_, e) =>
        {
            if (e.Packet is Aprs.Core.WeatherAprsPacket wp)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    rt.MainViewModel.Weather.AcceptWeatherPacket(wp, e.Source));
            }
        };
    }

    private static void WireReadiness(DesktopRuntime rt)
    {
        void Refresh()
        {
            rt.MainViewModel.Readiness.Refresh(
                store:               JsonAppSettingsStore.Default,
                connectionState:     rt.ConnectionState,
                hasGpsFix:           rt.GpsCoordinator.HasValidFix,
                lastBeaconAt:        rt.BeaconService.LastBeaconAt,
                exerciseModeActive:  rt.TransmitAuthority.IsInhibited);
        }

        // Refresh immediately, then every 5 seconds.
        Refresh();
        var timer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(5),
            Avalonia.Threading.DispatcherPriority.Background,
            (_, _) => Refresh());
        timer.Start();
    }

    private DesktopRuntime? runtime;
    public DesktopRuntime? Runtime => runtime;

    private bool isDarkMode;
    public bool IsDarkMode => isDarkMode;

    public void ToggleDarkMode()
    {
        isDarkMode = !isDarkMode;
        RequestedThemeVariant = isDarkMode
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
        // Persist so the preference survives restarts.
        JsonAppSettingsStore.Default.Update(s => s with { DarkMode = isDarkMode });
    }

    /// <summary>Applies the persisted dark mode preference on startup before any window opens.</summary>
    public void ApplyPersistedTheme()
    {
        isDarkMode = JsonAppSettingsStore.Default.Load().DarkMode;
        RequestedThemeVariant = isDarkMode
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Apply saved theme preference before any window opens.
        ApplyPersistedTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Design.IsDesignMode)
            {
                // The XAML previewer uses sample data only.
                desktop.MainWindow = new MainWindow
                {
                    DataContext = MainWindowViewModel.CreateDesignTime()
                };
            }
            else
            {
                desktop.ShutdownRequested += OnShutdownRequested;

                // First run (no station profile yet): collect the operator's callsign and QTH
                // before starting, so the app works for anyone, not just a preset station.
                if (StationProfile.Load().IsConfigured)
                {
                    // Already configured: framework shows the main window after this returns.
                    runtime = DesktopRuntime.Create();
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = runtime.MainViewModel
                    };
                    runtime.Start();
                    runtime.MainViewModel.StationSetup.SettingsSaved += (_, _) =>
                        runtime.BeaconService.ApplySettings(JsonAppSettingsStore.Default.Load());
                    WireSoundAlerts(runtime);
                    WireMessageToast(runtime);
                    WireGpsWriteback(runtime);
                    WireMessageAck(runtime);
                    WireRfDiagnostics(runtime);
                    WireGpsStatus(runtime);
                    WireConnectionWatchdog(runtime);
                    WireReadiness(runtime);
                    WireWeather(runtime);
                    WireTrails(runtime, (MainWindow)desktop.MainWindow!);
                    WireNetControl(runtime);
                    WireNwsAlerts(runtime);
                }
                else
                {
                    var setup = new SetupWindow();
                    setup.SetupCompleted += () =>
                    {
                        runtime = DesktopRuntime.Create();
                        var mainWindow = new MainWindow
                        {
                            DataContext = runtime.MainViewModel
                        };

                        // The framework already auto-showed the setup window, so the main
                        // window must be shown explicitly before the setup window closes.
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();
                        runtime.Start();
                        runtime.MainViewModel.StationSetup.SettingsSaved += (_, _) =>
                            runtime.BeaconService.ApplySettings(JsonAppSettingsStore.Default.Load());
                        WireSoundAlerts(runtime);
                        WireMessageToast(runtime);
                        WireGpsWriteback(runtime);
                        WireMessageAck(runtime);
                        WireRfDiagnostics(runtime);
                        WireGpsStatus(runtime);
                        WireConnectionWatchdog(runtime);
                        WireReadiness(runtime);
                        WireWeather(runtime);
                        WireNetControl(runtime);
                        WireNwsAlerts(runtime);
                        // Trail wiring happens after main window is shown (setup.Close triggers it).
                        setup.Closed += (_, _) =>
                        {
                            if (desktop.MainWindow is MainWindow mw)
                                WireTrails(runtime, mw);
                        };
                        setup.Close();
                    };

                    desktop.MainWindow = setup;
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (runtime is not null)
        {
            await runtime.DisposeAsync();
            runtime = null;
        }
    }

    private static void WireSoundAlerts(DesktopRuntime rt)
    {
        var sound = new SoundAlertService(JsonAppSettingsStore.Default);

        // Message received → play MessageReceived sound.
        // The MessageCenterViewModel is wired to a design-time service in DesktopRuntime
        // (not yet fully live), so we hook the ingestion pipeline directly via the
        // SettingsTransmitPolicyContext's store — simplest path that doesn't require
        // restructuring the composition root.
        // For now, subscribe to the ingestion PacketIngested event and detect message
        // packets by checking if the inbox grew. When MessageCenterViewModel is fully
        // wired to a live IAprsMessageStoreService, this can be replaced with a direct
        // IncomingMessageReceived subscription.
        var lastInboxCount = 0;
        rt.Coordinator.PacketIngested += (_, _) =>
        {
            var count = rt.MainViewModel.MessageCenter.InboxCount;
            if (count > lastInboxCount)
            {
                lastInboxCount = count;
                sound.Play(AlertSound.MessageReceived, $"msg-{count}");
            }
        };

        // Connection events
        var lastState = Aprs.Transport.AprsIsConnectionState.Disconnected;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            while (true)
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1));
                var state = rt.ConnectionState;
                if (state != lastState)
                {
                    if (state == Aprs.Transport.AprsIsConnectionState.Connected)
                        sound.Play(AlertSound.Connected, $"conn-{DateTimeOffset.UtcNow.Ticks}");
                    else if (lastState == Aprs.Transport.AprsIsConnectionState.Connected)
                        sound.Play(AlertSound.Disconnected, $"disc-{DateTimeOffset.UtcNow.Ticks}");
                    lastState = state;
                }
            }
        });
    }
}
