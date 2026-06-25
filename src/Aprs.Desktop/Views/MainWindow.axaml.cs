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
}
