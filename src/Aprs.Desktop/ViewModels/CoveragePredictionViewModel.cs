using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Manages PHG coverage overlays on the map. Operators can show coverage
/// circles for their own station (using its configured PHG data) or enter
/// any callsign, position, and PHG string manually.
/// </summary>
public sealed class CoveragePredictionViewModel : INotifyPropertyChanged
{
    private readonly ILocalStationProfileService profileService;
    private readonly IStationDatabase? stationDatabase;

    private string callsign     = string.Empty;
    private string phgString    = string.Empty;
    private string latText      = string.Empty;
    private string lonText      = string.Empty;
    private string statusText   = string.Empty;
    private CoverageOverlayViewModel? selectedOverlay;

    // PHG Calculator fields
    private double calcPowerW     = 25.0;
    private double calcHaatFt     = 80.0;
    private double calcGainDbd    = 3.0;
    private int    calcDirCode    = 0;
    private string calcResult     = string.Empty;
    private string calcPhgString  = string.Empty;

    public CoveragePredictionViewModel(ILocalStationProfileService profileService,
        IStationDatabase? stationDatabase = null)
    {
        this.profileService  = profileService;
        this.stationDatabase = stationDatabase;
        Overlays                  = [];
        AddCommand                = new DesktopCommand(AddOverlay);
        AddOwnCommand             = new DesktopCommand(AddOwnStation);
        FromSelectedStationCommand = new DesktopCommand(FillFromSelectedStation);
        RemoveCommand             = new DesktopCommand(RemoveSelected, () => SelectedOverlay is not null);
        ClearCommand              = new DesktopCommand(ClearAll);
        CalculatePhgCommand       = new DesktopCommand(CalculatePhg);
        PreFillOwn();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<IReadOnlyList<CoverageOverlayViewModel>>? OverlaysChanged;

    public ObservableCollection<CoverageOverlayViewModel> Overlays { get; }

    public CoverageOverlayViewModel? SelectedOverlay
    {
        get => selectedOverlay;
        set { if (selectedOverlay != value) { selectedOverlay = value; OnPropertyChanged(); } }
    }

    public string Callsign
    {
        get => callsign;
        set { if (callsign != value) { callsign = value; OnPropertyChanged(); } }
    }

    public string PhgString
    {
        get => phgString;
        set { if (phgString != value) { phgString = value; OnPropertyChanged(); UpdatePreview(); } }
    }

    public string LatText
    {
        get => latText;
        set { if (latText != value) { latText = value; OnPropertyChanged(); } }
    }

    public string LonText
    {
        get => lonText;
        set { if (lonText != value) { lonText = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand AddCommand                { get; }
    public DesktopCommand AddOwnCommand             { get; }
    public DesktopCommand FromSelectedStationCommand { get; }
    public DesktopCommand RemoveCommand             { get; }
    public DesktopCommand CalculatePhgCommand       { get; }
    public DesktopCommand ClearCommand              { get; }

    // ── PHG Calculator properties ──────────────────────────────────────────

    public double CalcPowerW
    {
        get => calcPowerW;
        set { if (calcPowerW != value) { calcPowerW = value; OnPropertyChanged(); } }
    }

    public double CalcHaatFt
    {
        get => calcHaatFt;
        set { if (calcHaatFt != value) { calcHaatFt = value; OnPropertyChanged(); } }
    }

    public double CalcGainDbd
    {
        get => calcGainDbd;
        set { if (calcGainDbd != value) { calcGainDbd = value; OnPropertyChanged(); } }
    }

    public int CalcDirCode
    {
        get => calcDirCode;
        set { if (calcDirCode != value) { calcDirCode = value; OnPropertyChanged(); } }
    }

    public string CalcResult
    {
        get => calcResult;
        private set { if (calcResult != value) { calcResult = value; OnPropertyChanged(); } }
    }

    public string CalcPhgString
    {
        get => calcPhgString;
        private set { if (calcPhgString != value) { calcPhgString = value; OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> DirectivityOptions { get; } =
        ["0 — Omni", "1 — NE", "2 — E", "3 — SE", "4 — S", "5 — SW", "6 — W", "7 — NW", "8 — N"];

    private void CalculatePhg()
    {
        var calc = Services.PhgCoverageService.Calculate(
            CalcPowerW, CalcHaatFt, CalcGainDbd, CalcDirCode);

        CalcPhgString = calc.PhgString;
        PhgString     = calc.PhgString; // also copy to the overlay form

        CalcResult =
            $"PHG string:      {calc.PhgString}\n" +
            $"Encoded power:   {calc.EncodedPowerW}W (entered: {calc.ActualPowerW:F0}W)\n" +
            $"Encoded HAAT:    {calc.EncodedHaatFt}ft (entered: {calc.ActualHaatFt:F0}ft)\n" +
            $"Encoded gain:    {calc.EncodedGainDbd}dBd (entered: {calc.ActualGainDbd:F1}dBd)\n" +
            $"Directivity:     {calc.DirectivityLabel}\n" +
            $"Estimated range: {calc.RangeKm:F1} km ({calc.RangeKm / 1.60934:F1} miles)\n\n" +
            $"Note: PHG encoding uses discrete steps. The actual encoded values may\n" +
            $"differ slightly from what you entered. The PHG string has been copied\n" +
            $"to the overlay form above.";
    }

    /// <summary>The callsign of the currently selected station on the map — set by the window handler.</summary>
    public string? SelectedMapCallsign { get; set; }

    private void PreFillOwn()
    {
        var profile = profileService.GetCurrentProfile();
        Callsign  = profile.Callsign ?? string.Empty;
        PhgString = profile.PhgData ?? string.Empty;
        LatText   = profile.FixedLatitude?.ToString("F6") ?? string.Empty;
        LonText   = profile.FixedLongitude?.ToString("F6") ?? string.Empty;
        UpdatePreview();
    }

    private void AddOwnStation()
    {
        PreFillOwn();
        AddOverlay();
    }

    private void FillFromSelectedStation()
    {
        if (string.IsNullOrWhiteSpace(SelectedMapCallsign))
        {
            StatusText = "No station selected on the map. Click a station first, then open Coverage Prediction.";
            return;
        }

        var station = stationDatabase?.GetStation(SelectedMapCallsign);
        if (station is null)
        {
            StatusText = $"{SelectedMapCallsign} not found in station database.";
            return;
        }

        Callsign = station.Callsign;
        LatText  = station.Latitude?.ToString("F6") ?? string.Empty;
        LonText  = station.Longitude?.ToString("F6") ?? string.Empty;

        // PHG appears in the comment field as "PHG7370" or "PHGxxxx"
        var comment = station.Comment ?? string.Empty;
        var phgMatch = System.Text.RegularExpressions.Regex.Match(
            comment, @"PHG[0-9]{4}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (phgMatch.Success)
        {
            PhgString = phgMatch.Value.ToUpperInvariant();
            StatusText = $"Loaded {station.Callsign} — PHG found in comment: {PhgString}";
        }
        else
        {
            PhgString  = string.Empty;
            StatusText = $"Loaded {station.Callsign} — no PHG data in comment. Enter PHG string manually.";
        }
    }

    private void UpdatePreview()
    {
        var phg = Services.PhgCoverageService.ParsePhg(PhgString);
        StatusText = phg is not null
            ? Services.PhgCoverageService.FormatSummary(phg)
            : string.IsNullOrWhiteSpace(PhgString) ? string.Empty : "Invalid PHG string.";
    }

    private void AddOverlay()
    {
        var cs = Callsign.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cs)) { StatusText = "Enter a callsign."; return; }

        var phg = Services.PhgCoverageService.ParsePhg(PhgString);
        if (phg is null) { StatusText = "Enter a valid PHG string (e.g. PHG7370)."; return; }

        if (!double.TryParse(LatText, out var lat) || lat < -90 || lat > 90)
        { StatusText = "Enter a valid latitude."; return; }

        if (!double.TryParse(LonText, out var lon) || lon < -180 || lon > 180)
        { StatusText = "Enter a valid longitude."; return; }

        if (Overlays.Any(o => string.Equals(o.Callsign, cs, StringComparison.OrdinalIgnoreCase)))
        { StatusText = $"{cs} overlay already shown."; return; }

        var overlay = new CoverageOverlayViewModel(cs, lat, lon, phg);
        Overlays.Add(overlay);
        NotifyOverlaysChanged();
        StatusText = $"Added {cs} — {Services.PhgCoverageService.FormatSummary(phg)}";
    }

    private void RemoveSelected()
    {
        if (SelectedOverlay is null) return;
        Overlays.Remove(SelectedOverlay);
        SelectedOverlay = null;
        NotifyOverlaysChanged();
        StatusText = "Overlay removed.";
    }

    private void ClearAll()
    {
        Overlays.Clear();
        SelectedOverlay = null;
        NotifyOverlaysChanged();
        StatusText = "All overlays cleared.";
    }

    private void NotifyOverlaysChanged()
        => OverlaysChanged?.Invoke(this, Overlays.ToList());

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class CoverageOverlayViewModel
{
    public CoverageOverlayViewModel(string callsign, double lat, double lon,
                                    Services.PhgParameters phg)
    {
        Callsign  = callsign;
        Latitude  = lat;
        Longitude = lon;
        Phg       = phg;
        Summary   = Services.PhgCoverageService.FormatSummary(phg);
        Color     = "#2563eb";
    }

    public string                  Callsign  { get; }
    public double                  Latitude  { get; }
    public double                  Longitude { get; }
    public Services.PhgParameters  Phg       { get; }
    public string                  Summary   { get; }
    public string                  Color     { get; set; }
}
