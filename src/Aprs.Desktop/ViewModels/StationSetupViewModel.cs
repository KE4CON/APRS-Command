using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Mapping;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Editable view of the operator's station profile. Changes are staged in-memory and only written
/// to the settings store when the operator clicks Save. The symbol picker populates
/// <see cref="SymbolTable"/> and <see cref="SymbolCode"/> automatically when the operator selects
/// a symbol — they never type a raw code.
/// </summary>
public sealed class StationSetupViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private readonly AprsSymbolLookupService symbolService = AprsSymbolLookupService.Default;

    private string callsign = string.Empty;
    private int ssid;
    private double latitude;
    private double longitude;
    private int filterRadiusKm;
    private char symbolTable = '/';
    private char symbolCode = '-';
    private string stationComment = string.Empty;
    private string beaconPath = string.Empty;
    private int aprsIsBeaconMinutes;
    private int rfBeaconMinutes;
    private bool fixedStationMode;
    private bool transmitEnabled;
    private bool aprsIsTransmitEnabled;
    private bool rfTransmitEnabled;
    private string phgData = string.Empty;
    private AprsSymbol? selectedSymbol;
    private string statusText = string.Empty;

    public StationSetupViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        AvailableSymbols = new ObservableCollection<AprsSymbol>(
            symbolService.GetKnownSymbols().Where(s => s.IsPrimaryTable));
        SaveCommand   = new DesktopCommand(Save);
        RevertCommand = new DesktopCommand(Load);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Symbol picker ────────────────────────────────────────────────────────

    /// <summary>All primary-table symbols, grouped by category, for the picker.</summary>
    public ObservableCollection<AprsSymbol> AvailableSymbols { get; }

    /// <summary>
    /// The currently selected symbol. Setting this automatically updates
    /// <see cref="SymbolTable"/> and <see cref="SymbolCode"/>.
    /// </summary>
    public AprsSymbol? SelectedSymbol
    {
        get => selectedSymbol;
        set
        {
            if (ReferenceEquals(selectedSymbol, value)) return;
            selectedSymbol = value;
            if (value is not null)
            {
                SymbolTable = value.SymbolTableIdentifier;
                SymbolCode  = value.SymbolCode;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SymbolDisplay));
            OnPropertyChanged(nameof(SymbolDescription));
        }
    }

    /// <summary>Human-readable display of the current symbol code, e.g. "House / home station (/-)".</summary>
    public string SymbolDisplay => selectedSymbol is not null
        ? $"{selectedSymbol.Description}  ({symbolTable}{symbolCode})"
        : $"{symbolTable}{symbolCode}";

    /// <summary>Plain description of the selected symbol for the UI label.</summary>
    public string SymbolDescription => selectedSymbol?.Description ?? "Select a symbol above";

    // ── Station identity ─────────────────────────────────────────────────────

    public string Callsign
    {
        get => callsign;
        set { if (callsign != value) { callsign = value; OnPropertyChanged(); } }
    }

    public int Ssid
    {
        get => ssid;
        set { if (ssid != value) { ssid = value; OnPropertyChanged(); } }
    }

    public double Latitude
    {
        get => latitude;
        set { if (latitude != value) { latitude = value; OnPropertyChanged(); } }
    }

    public double Longitude
    {
        get => longitude;
        set { if (longitude != value) { longitude = value; OnPropertyChanged(); } }
    }

    public int FilterRadiusKm
    {
        get => filterRadiusKm;
        set { if (filterRadiusKm != value) { filterRadiusKm = value; OnPropertyChanged(); } }
    }

    // ── Symbol (set automatically by SelectedSymbol) ─────────────────────────

    public char SymbolTable
    {
        get => symbolTable;
        private set { if (symbolTable != value) { symbolTable = value; OnPropertyChanged(); } }
    }

    public char SymbolCode
    {
        get => symbolCode;
        private set { if (symbolCode != value) { symbolCode = value; OnPropertyChanged(); } }
    }

    // ── Beacon settings ──────────────────────────────────────────────────────

    public string StationComment
    {
        get => stationComment;
        set { if (stationComment != value) { stationComment = value; OnPropertyChanged(); } }
    }

    public string BeaconPath
    {
        get => beaconPath;
        set { if (beaconPath != value) { beaconPath = value; OnPropertyChanged(); } }
    }

    public int AprsIsBeaconMinutes
    {
        get => aprsIsBeaconMinutes;
        set { if (aprsIsBeaconMinutes != value) { aprsIsBeaconMinutes = value; OnPropertyChanged(); } }
    }

    public int RfBeaconMinutes
    {
        get => rfBeaconMinutes;
        set { if (rfBeaconMinutes != value) { rfBeaconMinutes = value; OnPropertyChanged(); } }
    }

    public bool FixedStationMode
    {
        get => fixedStationMode;
        set { if (fixedStationMode != value) { fixedStationMode = value; OnPropertyChanged(); } }
    }

    public string PhgData
    {
        get => phgData;
        set { if (phgData != value) { phgData = value; OnPropertyChanged(); } }
    }

    // ── Transmit safety ──────────────────────────────────────────────────────

    public bool TransmitEnabled
    {
        get => transmitEnabled;
        set { if (transmitEnabled != value) { transmitEnabled = value; OnPropertyChanged(); } }
    }

    public bool AprsIsTransmitEnabled
    {
        get => aprsIsTransmitEnabled;
        set { if (aprsIsTransmitEnabled != value) { aprsIsTransmitEnabled = value; OnPropertyChanged(); } }
    }

    public bool RfTransmitEnabled
    {
        get => rfTransmitEnabled;
        set { if (rfTransmitEnabled != value) { rfTransmitEnabled = value; OnPropertyChanged(); } }
    }

    // ── Status / commands ────────────────────────────────────────────────────

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand { get; }
    public DesktopCommand RevertCommand { get; }

    public void Load()
    {
        var p = store.Load().Station;
        Callsign             = p.Callsign;
        Ssid                 = p.Ssid;
        Latitude             = p.Latitude;
        Longitude            = p.Longitude;
        FilterRadiusKm       = p.FilterRadiusKm;
        symbolTable          = p.SymbolTable;
        symbolCode           = p.SymbolCode;
        StationComment       = p.StationComment;
        BeaconPath           = p.BeaconPath;
        AprsIsBeaconMinutes  = p.AprsIsBeaconMinutes;
        RfBeaconMinutes      = p.RfBeaconMinutes;
        FixedStationMode     = p.FixedStationMode;
        TransmitEnabled      = p.TransmitEnabled;
        AprsIsTransmitEnabled = p.AprsIsTransmitEnabled;
        RfTransmitEnabled    = p.RfTransmitEnabled;
        PhgData              = p.PhgData ?? string.Empty;

        // Sync symbol picker selection to the loaded symbol
        selectedSymbol = AvailableSymbols.FirstOrDefault(
            s => s.SymbolTableIdentifier == symbolTable && s.SymbolCode == symbolCode);
        OnPropertyChanged(nameof(SelectedSymbol));
        OnPropertyChanged(nameof(SymbolDisplay));
        OnPropertyChanged(nameof(SymbolDescription));

        StatusText = "Loaded.";
    }

    public event EventHandler? SettingsSaved;

    public void Save()
    {
        var profile = new StationProfile(
            Callsign:              Callsign.Trim().ToUpperInvariant(),
            Ssid:                  Ssid,
            Latitude:              Latitude,
            Longitude:             Longitude,
            FilterRadiusKm:        FilterRadiusKm,
            SymbolTable:           symbolTable,
            SymbolCode:            symbolCode,
            StationComment:        StationComment,
            BeaconPath:            BeaconPath,
            AprsIsBeaconMinutes:   AprsIsBeaconMinutes,
            RfBeaconMinutes:       RfBeaconMinutes,
            FixedStationMode:      FixedStationMode,
            TransmitEnabled:       TransmitEnabled,
            AprsIsTransmitEnabled: AprsIsTransmitEnabled,
            RfTransmitEnabled:     RfTransmitEnabled,
            PhgData:               string.IsNullOrWhiteSpace(PhgData) ? null : PhgData.Trim());
        store.Update(s => s with { Station = profile });
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        StatusText = "Saved.";
    }

    public static StationSetupViewModel CreateDesignTime()
    {
        var store = new InMemoryAppSettingsStore(AppSettings.Default with
        {
            Station = StationProfile.Default with
            {
                Callsign = "KE4CON",
                Ssid = 7,
                Latitude = 39.0,
                Longitude = -84.5
            }
        });
        return new StationSetupViewModel(store);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
