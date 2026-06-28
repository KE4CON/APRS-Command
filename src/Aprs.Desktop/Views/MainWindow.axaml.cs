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
            vm.AboutRequested         -= OnAboutRequested;
            vm.DarkModeRequested      -= OnDarkModeRequested;
            vm.NetControlRequested    -= OnNetControlRequested;
            vm.NwsAlertsRequested     -= OnNwsAlertsRequested;
            vm.AfterActionRequested   -= OnAfterActionRequested;
            if (vm.Map is not null)
            {
                vm.Map.AlertStatusRequested     -= OnAlertStatusRequested;
                vm.Map.MeasureDistanceRequested -= OnMeasureDistanceRequested;
            }
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
            vm.AboutRequested         += OnAboutRequested;
            vm.DarkModeRequested      += OnDarkModeRequested;
            vm.NetControlRequested    += OnNetControlRequested;
            vm.NwsAlertsRequested     += OnNwsAlertsRequested;
            vm.AfterActionRequested   += OnAfterActionRequested;
            if (vm.Map is not null)
            {
                vm.Map.AlertStatusRequested     += OnAlertStatusRequested;
                vm.Map.MeasureDistanceRequested += OnMeasureDistanceRequested;
            }
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

    private void OnAboutRequested(object? s, EventArgs e)
        => new AboutWindow().ShowDialog(this);

    private void OnDarkModeRequested(object? s, EventArgs e)
        => (Application.Current as App)?.ToggleDarkMode();

    private void OnNetControlRequested(object? s, EventArgs e)
    {
        var win = new NetControlView { DataContext = vm?.NetControl };
        win.Show(this);
    }

    private void OnNwsAlertsRequested(object? s, EventArgs e)
    {
        var win = new NwsAlertsWindow { DataContext = vm?.NwsAlerts };
        win.Show(this);
    }

    private void OnAfterActionRequested(object? s, EventArgs e)
    {
        var rt = (Application.Current as App)?.Runtime;
        var aarVm = AfterActionExportViewModel.CreateFromRuntime(rt);
        var win = new AfterActionExportWindow { DataContext = aarVm };
        win.ShowDialog(this);
    }

    private void NwsBanner_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var win = new NwsAlertsWindow { DataContext = vm?.NwsAlerts };
        win.Show(this);
    }

    private void OnAlertStatusRequested(object? s, EventArgs e)
        => ShowWithState(new AlertsWindow { DataContext = vm });

    private async void OnMeasureDistanceRequested(object? s, EventArgs e)
    {
        // Measure distance and bearing between two callsigns in the station list.
        if (vm?.Map is null) return;
        var markers = vm.Map.Markers;
        if (markers.Count == 0)
        {
            await ShowBeaconResult("Measure Distance", "No stations are currently on the map.", success: false);
            return;
        }

        var callsigns = markers.Select(m => m.Callsign).OrderBy(c => c).ToArray();

        var dialog = new Avalonia.Controls.Window
        {
            Title = "Measure Distance",
            Width = 380,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        };

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Measure distance and bearing between two stations:",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A"))
        });

        var grid = new Avalonia.Controls.Grid();
        grid.ColumnDefinitions.Add(new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new Avalonia.Controls.ColumnDefinition { Width = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new Avalonia.Controls.ColumnDefinition { Width = Avalonia.Controls.GridLength.Auto });

        var fromBox = new Avalonia.Controls.ComboBox { ItemsSource = callsigns, IsEditable = true, PlaceholderText = "From", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var toBox   = new Avalonia.Controls.ComboBox { ItemsSource = callsigns, IsEditable = true, PlaceholderText = "To",   HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var calcBtn = new Avalonia.Controls.Button { Content = "Calculate", Padding = new Avalonia.Thickness(10, 6), Margin = new Avalonia.Thickness(6, 0, 0, 0) };

        Avalonia.Controls.Grid.SetColumn(fromBox, 0);
        Avalonia.Controls.Grid.SetColumn(toBox, 1);
        Avalonia.Controls.Grid.SetColumn(calcBtn, 2);
        grid.Children.Add(fromBox);
        grid.Children.Add(toBox);
        grid.Children.Add(calcBtn);
        panel.Children.Add(grid);

        var resultText = new Avalonia.Controls.TextBlock
        {
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#475569")),
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        panel.Children.Add(resultText);

        var closeBtn = new Avalonia.Controls.Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Padding = new Avalonia.Thickness(14, 6) };
        closeBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(closeBtn);
        dialog.Content = panel;

        calcBtn.Click += (_, _) =>
        {
            var from = (fromBox.SelectedItem as string ?? fromBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var to   = (toBox.SelectedItem   as string ?? toBox.Text   ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) { resultText.Text = "Please select both stations."; return; }

            var mFrom = markers.FirstOrDefault(m => string.Equals(m.Callsign, from, StringComparison.OrdinalIgnoreCase));
            var mTo   = markers.FirstOrDefault(m => string.Equals(m.Callsign, to,   StringComparison.OrdinalIgnoreCase));

            if (mFrom is null || mTo is null) { resultText.Text = $"{(mFrom is null ? from : to)} not found in station list."; return; }

            var distKm  = Aprs.Desktop.Services.GeoMath.HaversineKm(mFrom.Latitude, mFrom.Longitude, mTo.Latitude, mTo.Longitude);
            var distMi  = distKm * 0.621371;
            var bearing = Aprs.Desktop.Services.GeoMath.BearingDeg(mFrom.Latitude, mFrom.Longitude, mTo.Latitude, mTo.Longitude);
            resultText.Text = $"{from} → {to}\nDistance: {distKm:F1} km  ({distMi:F1} mi)\nBearing: {bearing:F0}° ({Aprs.Desktop.Services.GeoMath.CardinalBearing(bearing)})";
        };

        await dialog.ShowDialog(this);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(dLon) * Math.Cos(lat2 * Math.PI / 180);
        var x = Math.Cos(lat1 * Math.PI / 180) * Math.Sin(lat2 * Math.PI / 180) -
                Math.Sin(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }

    private static string CardinalBearing(double deg) => (int)(deg / 22.5 + 0.5) switch
    {
        0 or 16 => "N", 1 => "NNE", 2 => "NE", 3 => "ENE",
        4 => "E", 5 => "ESE", 6 => "SE", 7 => "SSE",
        8 => "S", 9 => "SSW", 10 => "SW", 11 => "WSW",
        12 => "W", 13 => "WNW", 14 => "NW", 15 => "NNW",
        _ => "N"
    };

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
