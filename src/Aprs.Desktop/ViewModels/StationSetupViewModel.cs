using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    private DistanceUnit distanceUnit = DistanceUnit.Miles;
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
    private int ring1Distance = 10;
    private int ring2Distance = 25;
    private int ring3Distance = 50;
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
        set { if (filterRadiusKm != value) { filterRadiusKm = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilterRadiusDisplay)); } }
    }

    /// <summary>
    /// The operator's preferred distance unit. Miles for US operators, Kilometres for others.
    /// The UI binds to this; the app converts to km behind the scenes wherever APRS-IS requires it.
    /// </summary>
    public DistanceUnit DistanceUnit
    {
        get => distanceUnit;
        set
        {
            if (distanceUnit != value)
            {
                // Convert the current display radius to the new unit before switching.
                var currentDisplay = FilterRadiusDisplay;
                distanceUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseMetric));
                OnPropertyChanged(nameof(FilterUnitLabel));
                // Restore the converted display value in the new unit.
                FilterRadiusDisplay = currentDisplay;
            }
        }
    }

    public bool UseMetric
    {
        get => distanceUnit == DistanceUnit.Kilometres;
        set => DistanceUnit = value ? DistanceUnit.Kilometres : DistanceUnit.Miles;
    }

    public string FilterUnitLabel => distanceUnit == DistanceUnit.Miles ? "miles" : "km";

    /// <summary>
    /// The filter radius in the operator's preferred unit. Converts to/from km on get/set.
    /// The UI binds to this instead of FilterRadiusKm directly.
    /// </summary>
    public int FilterRadiusDisplay
    {
        get => distanceUnit == DistanceUnit.Miles
            ? (int)Math.Round(filterRadiusKm * 0.621371)
            : filterRadiusKm;
        set
        {
            var newKm = distanceUnit == DistanceUnit.Miles
                ? (int)Math.Round(value / 0.621371)
                : value;
            FilterRadiusKm = Math.Max(1, newKm);
            OnPropertyChanged();
        }
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

    public int Ring1Distance
    {
        get => ring1Distance;
        set { if (ring1Distance != value) { ring1Distance = Math.Max(1, value); OnPropertyChanged(); } }
    }

    public int Ring2Distance
    {
        get => ring2Distance;
        set { if (ring2Distance != value) { ring2Distance = Math.Max(1, value); OnPropertyChanged(); } }
    }

    public int Ring3Distance
    {
        get => ring3Distance;
        set { if (ring3Distance != value) { ring3Distance = Math.Max(1, value); OnPropertyChanged(); } }
    }

    // PHG encoder — four dropdowns that build the PHG string automatically.
    public IReadOnlyList<string> PhgPowerOptions { get; } =
        ["None", "0W (0)", "1W (1)", "4W (2)", "9W (3)", "16W (4)", "25W (5)", "36W (6)", "49W (7)", "64W (8)", "81W (9)"];

    public IReadOnlyList<string> PhgHeightOptions { get; } =
        ["None", "10ft (0)", "20ft (1)", "40ft (2)", "80ft (3)", "160ft (4)", "320ft (5)", "640ft (6)", "1280ft (7)", "2560ft (8)", "5120ft (9)"];

    public IReadOnlyList<string> PhgGainOptions { get; } =
        ["None", "0dB (0)", "1dB (1)", "2dB (2)", "3dB (3)", "4dB (4)", "5dB (5)", "6dB (6)", "7dB (7)", "8dB (8)", "9dB (9)"];

    public IReadOnlyList<string> PhgDirectivityOptions { get; } =
        ["None", "Omni (0)", "NE (1)", "E (2)", "SE (3)", "S (4)", "SW (5)", "W (6)", "NW (7)", "N (8)"];

    public void ApplyPhgFromDropdowns(int powerIndex, int heightIndex, int gainIndex, int directivityIndex)
    {
        // Index 0 = "None" — if any is None, clear the whole PHG string.
        if (powerIndex <= 0 || heightIndex <= 0 || gainIndex <= 0 || directivityIndex <= 0)
        {
            PhgData = string.Empty;
            return;
        }
        // Subtract 1 to convert from 1-based dropdown index to 0-based PHG digit.
        PhgData = $"PHG{powerIndex - 1}{heightIndex - 1}{gainIndex - 1}{directivityIndex - 1}";
    }

    public (int Power, int Height, int Gain, int Directivity) ParsePhgToDropdownIndices()
    {
        // Try to parse existing PHG string like "PHG5380" back to dropdown indices.
        if (phgData.Length == 7 && phgData.StartsWith("PHG", StringComparison.OrdinalIgnoreCase)
            && phgData[3..].All(char.IsDigit))
        {
            return (phgData[3] - '0' + 1, phgData[4] - '0' + 1, phgData[5] - '0' + 1, phgData[6] - '0' + 1);
        }
        return (0, 0, 0, 0);
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
        distanceUnit         = p.DistanceUnit;
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
        ring1Distance        = p.Ring1Distance;
        ring2Distance        = p.Ring2Distance;
        ring3Distance        = p.Ring3Distance;

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
            BeaconPath:            SanitizeBeaconPath(BeaconPath),
            AprsIsBeaconMinutes:   AprsIsBeaconMinutes,
            RfBeaconMinutes:       RfBeaconMinutes,
            FixedStationMode:      FixedStationMode,
            TransmitEnabled:       TransmitEnabled,
            AprsIsTransmitEnabled: AprsIsTransmitEnabled,
            RfTransmitEnabled:     RfTransmitEnabled,
            PhgData:               string.IsNullOrWhiteSpace(PhgData) ? null : PhgData.Trim(),
            DistanceUnit:          DistanceUnit,
            Ring1Distance:         Ring1Distance,
            Ring2Distance:         Ring2Distance,
            Ring3Distance:         Ring3Distance);
        store.Update(s => s with { Station = profile });
        SettingsSaved?.Invoke(this, EventArgs.Empty);

        // Reflect the sanitized path back into the UI field.
        var sanitized = SanitizeBeaconPath(BeaconPath);
        if (sanitized != BeaconPath)
        {
            BeaconPath = sanitized;
            StatusText = "Saved. Beacon path was corrected — spaces removed (APRS paths must not contain spaces).";
        }
        else
        {
            StatusText = "Saved.";
        }
    }

    /// <summary>
    /// Sanitizes an APRS beacon path by removing all spaces and uppercasing.
    /// APRS path elements like WIDE1-1 must not contain spaces — a path like
    /// "WIDE 1-1,WIDE 2-1" is invalid and will be silently dropped by APRS-IS.
    /// </summary>
    private static string SanitizeBeaconPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        // Remove all whitespace and uppercase the result.
        return new string(path.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
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
