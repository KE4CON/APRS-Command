using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aprs.Desktop.Composition;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Audio;
using Aprs.Desktop.ViewModels;
using Aprs.Desktop.Views;

namespace Aprs.Desktop;

public sealed partial class App : Application
{
    private DesktopRuntime? runtime;
    public DesktopRuntime? Runtime => runtime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
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
