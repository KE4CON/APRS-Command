using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aprs.Desktop.Composition;
using Aprs.Desktop.Configuration;
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
}
