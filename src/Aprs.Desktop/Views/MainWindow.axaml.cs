using Avalonia;
using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
        => new MessagesWindow { DataContext = vm }.Show(this);

    private void OnObjectsRequested(object? s, EventArgs e)
        => new ObjectsWindow { DataContext = vm }.Show(this);

    private void OnWeatherRequested(object? s, EventArgs e)
        => new WeatherWindow { DataContext = vm }.Show(this);

    private void OnEventsRequested(object? s, EventArgs e)
        => new EventsWindow { DataContext = vm }.Show(this);

    private void OnEventBusRequested(object? s, EventArgs e)
        => new EventBusWindow { DataContext = vm }.Show(this);

    private void OnReplayRequested(object? s, EventArgs e)
        => new ReplayWindow { DataContext = vm }.Show(this);

    private void OnRfDiagnosticsRequested(object? s, EventArgs e)
        => new RfDiagnosticsWindow { DataContext = vm }.Show(this);

    private void OnAlertsRequested(object? s, EventArgs e)
        => new AlertsWindow { DataContext = vm }.Show(this);

    private void OnStationListRequested(object? s, EventArgs e)
        => new StationListWindow { DataContext = vm }.Show(this);

    private void OnRawPacketsRequested(object? s, EventArgs e)
        => new RawPacketsWindow { DataContext = vm }.Show(this);

    private void OnSettingsRequested(object? s, EventArgs e)
        => new SettingsWindow { DataContext = vm }.Show(this);

    private void OnHelpRequested(object? s, EventArgs e)
        => new HelpWindow { DataContext = HelpViewModel.CreateDefault() }.Show(this);

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
