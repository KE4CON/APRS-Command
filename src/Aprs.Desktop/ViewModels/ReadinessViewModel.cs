using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Transport;

namespace Aprs.Desktop.ViewModels;

public sealed class ReadinessItem : INotifyPropertyChanged
{
    private bool isReady;
    private string detail = string.Empty;

    public string Label { get; init; } = string.Empty;
    public string ReadyIcon   => IsReady ? "✓" : "✗";
    public string ReadyColor  => IsReady ? "#15803d" : "#b91c1b";

    public bool IsReady
    {
        get => isReady;
        set { if (isReady != value) { isReady = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReadyIcon)); OnPropertyChanged(nameof(ReadyColor)); } }
    }

    public string Detail
    {
        get => detail;
        set { if (detail != value) { detail = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// Pre-operation readiness checklist — shows go/no-go status for the five key indicators
/// an operator should confirm before beginning an EmComm activation or exercise.
/// </summary>
public sealed class ReadinessViewModel : INotifyPropertyChanged
{
    public ReadinessItem CallsignConfigured { get; } = new() { Label = "Callsign configured" };
    public ReadinessItem AprsIsConnected    { get; } = new() { Label = "APRS-IS connected" };
    public ReadinessItem GpsFixAcquired     { get; } = new() { Label = "GPS fix acquired" };
    public ReadinessItem BeaconTransmitted  { get; } = new() { Label = "Beacon transmitted" };
    public ReadinessItem ExerciseMode       { get; } = new() { Label = "Exercise mode OFF (transmit allowed)" };

    public ObservableCollection<ReadinessItem> Items { get; }

    private bool allReady;
    public bool AllReady
    {
        get => allReady;
        private set { if (allReady != value) { allReady = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryText)); OnPropertyChanged(nameof(SummaryColor)); } }
    }

    public string SummaryText  => AllReady ? "Ready to operate" : "Not ready — check items below";
    public string SummaryColor => AllReady ? "#15803d" : "#b91c1b";

    public ReadinessViewModel()
    {
        Items = [CallsignConfigured, AprsIsConnected, GpsFixAcquired, BeaconTransmitted, ExerciseMode];
        foreach (var item in Items)
            item.PropertyChanged += (_, _) => Recalculate();
    }

    /// <summary>Called by the runtime on a 5-second timer to refresh all indicators.</summary>
    public void Refresh(
        IAppSettingsStore store,
        AprsIsConnectionState connectionState,
        bool hasGpsFix,
        DateTimeOffset? lastBeaconAt,
        bool exerciseModeActive)
    {
        var settings = store.Load();
        var callsign = settings.Station.Callsign;

        CallsignConfigured.IsReady = !string.IsNullOrWhiteSpace(callsign)
            && !callsign.Equals("N0CALL", StringComparison.OrdinalIgnoreCase);
        CallsignConfigured.Detail  = CallsignConfigured.IsReady ? callsign.ToUpperInvariant() : "Not set — open Station Setup";

        var aprsIsPort = settings.Connections.Ports.FirstOrDefault(p => p.Type == Configuration.ConnectionPortType.AprsIs);
        var aprsIsHost = aprsIsPort?.Configuration.AprsIs?.ServerHost ?? "APRS-IS";

        AprsIsConnected.IsReady = connectionState == AprsIsConnectionState.Connected;
        AprsIsConnected.Detail  = connectionState switch
        {
            AprsIsConnectionState.Connected    => $"Connected to {aprsIsHost}",
            AprsIsConnectionState.Connecting   => "Connecting…",
            AprsIsConnectionState.Disconnected => "Disconnected — check Connections settings",
            _                                  => connectionState.ToString()
        };

        GpsFixAcquired.IsReady = hasGpsFix;
        GpsFixAcquired.Detail  = hasGpsFix ? "GPS fix active" : settings.Gps.Enabled ? "GPS enabled but no fix yet" : "GPS not enabled — using manual position";

        BeaconTransmitted.IsReady = lastBeaconAt.HasValue
            && DateTimeOffset.UtcNow - lastBeaconAt.Value < TimeSpan.FromMinutes(30);
        BeaconTransmitted.Detail  = lastBeaconAt.HasValue
            ? $"Last beacon: {lastBeaconAt.Value.ToLocalTime():HH:mm:ss}"
            : "No beacon sent yet — click Beacon Now";

        ExerciseMode.IsReady  = !exerciseModeActive;
        ExerciseMode.Detail   = exerciseModeActive ? "Exercise mode ON — all transmit inhibited" : "Transmit allowed";
    }

    public static ReadinessViewModel CreateDesignTime()
    {
        var vm = new ReadinessViewModel();
        vm.CallsignConfigured.IsReady = true;  vm.CallsignConfigured.Detail = "KE4CON";
        vm.AprsIsConnected.IsReady    = true;  vm.AprsIsConnected.Detail    = "Connected to rotate.aprs2.net";
        vm.GpsFixAcquired.IsReady     = false; vm.GpsFixAcquired.Detail     = "No GPS fix yet";
        vm.BeaconTransmitted.IsReady  = true;  vm.BeaconTransmitted.Detail  = "Last beacon: 14:22:01";
        vm.ExerciseMode.IsReady       = true;  vm.ExerciseMode.Detail       = "Transmit allowed";
        return vm;
    }

    private void Recalculate() => AllReady = Items.All(i => i.IsReady);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
