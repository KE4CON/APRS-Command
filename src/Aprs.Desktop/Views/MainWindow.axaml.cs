using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.ViewModels;
using Aprs.Transport;

namespace Aprs.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? vm;
    private DispatcherTimer? statusTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Restore MainWindow position/size from last session.
        var store = JsonAppSettingsStore.Default;
        Opened  += (_, _) => WindowStateService.Restore(this, store);
        Closing += (_, _) => WindowStateService.Save(this, store);

        // Poll connection state and transmit inhibit every second to keep badges current.
        statusTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => RefreshStatusBadges());
        statusTimer.Start();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (vm is not null)
        {
            vm.MessagesRequested      -= OnMessagesRequested;
            vm.ObjectsRequested       -= OnObjectsRequested;
            vm.WeatherRequested       -= OnWeatherRequested;
            vm.EventsRequested        -= OnEventsRequested;
            vm.EventBusRequested      -= OnEventBusRequested;
            vm.ReplayRequested        -= OnReplayRequested;
            vm.RfDiagnosticsRequested -= OnRfDiagnosticsRequested;
            vm.AlertsRequested        -= OnAlertsRequested;
            vm.StationListRequested   -= OnStationListRequested;
            vm.RawPacketsRequested    -= OnRawPacketsRequested;
            vm.SettingsRequested      -= OnSettingsRequested;
            vm.HelpRequested          -= OnHelpRequested;
            vm.BeaconNowRequested     -= OnBeaconNowRequested;
            vm.ExerciseModeRequested  -= OnExerciseModeRequested;
        }

        vm = DataContext as MainWindowViewModel;

        if (vm is not null)
        {
            vm.MessagesRequested      += OnMessagesRequested;
            vm.ObjectsRequested       += OnObjectsRequested;
            vm.WeatherRequested       += OnWeatherRequested;
            vm.EventsRequested        += OnEventsRequested;
            vm.EventBusRequested      += OnEventBusRequested;
            vm.ReplayRequested        += OnReplayRequested;
            vm.RfDiagnosticsRequested += OnRfDiagnosticsRequested;
            vm.AlertsRequested        += OnAlertsRequested;
            vm.StationListRequested   += OnStationListRequested;
            vm.RawPacketsRequested    += OnRawPacketsRequested;
            vm.SettingsRequested      += OnSettingsRequested;
            vm.HelpRequested          += OnHelpRequested;
            vm.BeaconNowRequested     += OnBeaconNowRequested;
            vm.ExerciseModeRequested  += OnExerciseModeRequested;
        }
    }

    private void OnMessagesRequested(object? s, EventArgs e)
        => ShowWithState(new MessagesWindow { DataContext = vm });

    private void OnObjectsRequested(object? s, EventArgs e)
        => ShowWithState(new ObjectsWindow { DataContext = vm });

    private void OnWeatherRequested(object? s, EventArgs e)
        => ShowWithState(new WeatherWindow { DataContext = vm });

    private void OnEventsRequested(object? s, EventArgs e)
        => ShowWithState(new EventsWindow { DataContext = vm });

    private void OnEventBusRequested(object? s, EventArgs e)
        => ShowWithState(new EventBusWindow { DataContext = vm });

    private void OnReplayRequested(object? s, EventArgs e)
        => ShowWithState(new ReplayWindow { DataContext = vm });

    private void OnRfDiagnosticsRequested(object? s, EventArgs e)
        => ShowWithState(new RfDiagnosticsWindow { DataContext = vm });

    private void OnAlertsRequested(object? s, EventArgs e)
        => ShowWithState(new AlertsWindow { DataContext = vm });

    private void OnStationListRequested(object? s, EventArgs e)
        => ShowWithState(new StationListWindow { DataContext = vm });

    private void OnRawPacketsRequested(object? s, EventArgs e)
        => ShowWithState(new RawPacketsWindow { DataContext = vm });

    private void OnSettingsRequested(object? s, EventArgs e)
        => ShowWithState(new SettingsWindow { DataContext = vm });

    private void OnHelpRequested(object? s, EventArgs e)
        => new HelpWindow { DataContext = HelpViewModel.CreateDefault() }.Show(this);

    /// <summary>
    /// Shows a feature window with its saved position/size restored, and saves its state on close.
    /// </summary>
    private void ShowWithState(Window window)
    {
        var store = JsonAppSettingsStore.Default;
        WindowStateService.Restore(window, store);
        window.Closing += (_, _) => WindowStateService.Save(window, store);
        window.Show(this);
    }

    private void RefreshStatusBadges()
    {
        var runtime = (Application.Current as App)?.Runtime;
        if (runtime is null) return;

        // APRS-IS badge
        var state = runtime.ConnectionState;
        var (aprsText, aprsBg, aprsBorder) = state switch
        {
            AprsIsConnectionState.Connected    => ("APRS-IS Connected",    "#0F3D2E", "#22C55E"),
            AprsIsConnectionState.Connecting   => ("APRS-IS Connecting…",  "#1E293B", "#F59E0B"),
            AprsIsConnectionState.Reconnecting => ("APRS-IS Reconnecting…","#1E293B", "#F59E0B"),
            AprsIsConnectionState.Faulted      => ("APRS-IS Error",        "#3D0F0F", "#F87171"),
            _                                  => ("APRS-IS Offline",      "#1E293B", "#64748B")
        };
        AprsIsBadgeText.Text = aprsText;
        AprsIsBadgeBorder.Background     = new SolidColorBrush(Color.Parse(aprsBg));
        AprsIsBadgeBorder.BorderBrush    = new SolidColorBrush(Color.Parse(aprsBorder));

        // TX badge — only update when NOT in exercise mode (exercise mode sets its own text)
        if (!runtime.IsTransmitInhibited)
        {
            TxBadgeText.Text = "TX Disabled";
            TxBadgeBorder.Background  = new SolidColorBrush(Color.Parse("#123B32"));
            TxBadgeBorder.BorderBrush = new SolidColorBrush(Color.Parse("#2DD4BF"));
        }
    }

    private void OnExerciseModeRequested(object? s, EventArgs e)
    {
        var authority = (Application.Current as App)?.Runtime?.TransmitAuthority;
        if (authority is null) return;

        if (authority.IsInhibited)
        {
            authority.Release();
            TxBadgeText.Text = "TX Enabled";
            TxBadgeBorder.Background = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#123B32"));
            TxBadgeBorder.BorderBrush = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#2DD4BF"));
        }
        else
        {
            authority.Inhibit("Exercise mode — all transmit inhibited");
            TxBadgeText.Text = "EXERCISE — TX INHIBITED";
            TxBadgeBorder.Background = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#7F1D1D"));
            TxBadgeBorder.BorderBrush = new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#F87171"));
        }
    }

    private async void OnBeaconNowRequested(object? s, EventArgs e)
    {
        // Get the BeaconService from the runtime via the App.
        var runtime = (Application.Current as App)?.Runtime;
        if (runtime is null)
        {
            await ShowBeaconResult("Beacon Now", "The beacon engine is not running.", success: false);
            return;
        }

        var result = await runtime.BeaconService.BeaconNowAsync();

        var title = result.Transmitted ? "Beacon Transmitted" : "Beacon Not Transmitted";
        var message = result.Message ?? (result.Transmitted ? "Your position was sent to APRS-IS." : "Beacon was not transmitted.");

        if (result.ValidationErrors.Count > 0)
        {
            message += "\n\n" + string.Join("\n", result.ValidationErrors);
        }

        await ShowBeaconResult(title, message, success: result.Transmitted);
    }

    private async Task ShowBeaconResult(string title, string message, bool success)
    {
        // Simple message box using Avalonia's built-in dialog.
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 16 };
        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = success
                ? Avalonia.Media.Brushes.DarkGreen
                : Avalonia.Media.Brushes.DarkRed
        });
        var closeBtn = new Avalonia.Controls.Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        closeBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(closeBtn);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
    }
}
