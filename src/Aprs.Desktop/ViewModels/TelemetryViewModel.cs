using System.Collections.ObjectModel;
using System.ComponentModel;
using Aprs.Core;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Manages the Telemetry Monitor window — a live log of received APRS telemetry packets
/// (packet type T#) showing up to five analog values and eight digital bits per packet.
/// </summary>
public sealed class TelemetryViewModel : INotifyPropertyChanged
{
    private const int MaxRecords = 500;

    private string _filterCallsign = string.Empty;
    private TelemetryRecord? _selectedRecord;

    public TelemetryViewModel()
    {
        Records     = new ObservableCollection<TelemetryRecord>();
        Stations    = new ObservableCollection<string>();
        ClearCommand = new DesktopCommand(Clear);
    }

    // ── Live packet feed ──────────────────────────────────────────────────────

    /// <summary>Called from the App wiring layer whenever a TelemetryAprsPacket is decoded.</summary>
    public void AcceptPacket(TelemetryAprsPacket packet)
    {
        var record = new TelemetryRecord(packet);

        // Trim to max
        while (Records.Count >= MaxRecords)
            Records.RemoveAt(Records.Count - 1);

        Records.Insert(0, record);

        // Keep station list sorted and deduplicated
        if (!Stations.Contains(record.Callsign))
        {
            var sorted = Stations.Append(record.Callsign).OrderBy(c => c).ToList();
            Stations.Clear();
            foreach (var s in sorted) Stations.Add(s);
        }

        OnPropertyChanged(nameof(Summary));
    }

    // ── Bindable state ────────────────────────────────────────────────────────

    /// <summary>All telemetry records received this session, newest first.</summary>
    public ObservableCollection<TelemetryRecord> Records { get; }

    /// <summary>Unique callsigns that have sent telemetry this session.</summary>
    public ObservableCollection<string> Stations { get; }

    public DesktopCommand ClearCommand { get; }

    public TelemetryRecord? SelectedRecord
    {
        get => _selectedRecord;
        set { _selectedRecord = value; OnPropertyChanged(nameof(SelectedRecord)); }
    }

    public string FilterCallsign
    {
        get => _filterCallsign;
        set
        {
            _filterCallsign = value;
            OnPropertyChanged(nameof(FilterCallsign));
            OnPropertyChanged(nameof(FilteredRecords));
        }
    }

    /// <summary>Records filtered by the current callsign filter.</summary>
    public IEnumerable<TelemetryRecord> FilteredRecords =>
        string.IsNullOrWhiteSpace(FilterCallsign)
            ? Records
            : Records.Where(r => r.Callsign.Contains(FilterCallsign, StringComparison.OrdinalIgnoreCase));

    public string Summary => $"{Records.Count} telemetry packet{(Records.Count == 1 ? "" : "s")} · {Stations.Count} station{(Stations.Count == 1 ? "" : "s")}";

    public void Clear()
    {
        Records.Clear();
        Stations.Clear();
        SelectedRecord = null;
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(FilteredRecords));
    }

    // ── Design time ───────────────────────────────────────────────────────────

    public static TelemetryViewModel CreateDesignTime()
    {
        var vm = new TelemetryViewModel();
        var now = DateTimeOffset.UtcNow;
        // Fake records for the AXAML previewer
        vm.Records.Add(new TelemetryRecord(new TelemetryAprsPacket(
            "KE4CON-9>APRS:T#001,087,000,000,000,000,00000000",
            "KE4CON", 9, "APRS", [], "T#001,087,000,000,000,000,00000000",
            now, true, [], null, "001,087,000,000,000,000,00000000",
            1, [87, 0, 0, 0, 0], [false, false, false, false, false, false, false, false])));
        vm.Stations.Add("KE4CON-9");
        return vm;
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
