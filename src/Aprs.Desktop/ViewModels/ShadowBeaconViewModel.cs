using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aprs.Desktop.ViewModels;

public sealed class ShadowBeaconViewModel : INotifyPropertyChanged
{
    private Runtime.ShadowBeaconService? service;

    private string trackedCallsign = string.Empty;
    private string latitudeText    = string.Empty;
    private string longitudeText   = string.Empty;
    private string comment         = string.Empty;
    private string symbolTable     = "/";
    private string symbolCode      = ">";  // car default
    private string statusText      = string.Empty;
    private bool isTransmitting;

    public ShadowBeaconViewModel()
    {
        TransmitCommand = new DesktopCommand(async () => await TransmitAsync());
        RemoveCommand   = new DesktopCommand(RemoveSelected, () => SelectedStation is not null);
        ClearAllCommand = new DesktopCommand(ClearAll);
        ActiveStations  = [];
        RefreshList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetService(Runtime.ShadowBeaconService svc) { service = svc; }

    public ObservableCollection<ShadowStationRowViewModel> ActiveStations { get; }

    public ShadowStationRowViewModel? SelectedStation { get; set; }

    public string TrackedCallsign
    {
        get => trackedCallsign;
        set { if (trackedCallsign != value) { trackedCallsign = value; OnPropertyChanged(); } }
    }

    public string LatitudeText
    {
        get => latitudeText;
        set { if (latitudeText != value) { latitudeText = value; OnPropertyChanged(); } }
    }

    public string LongitudeText
    {
        get => longitudeText;
        set { if (longitudeText != value) { longitudeText = value; OnPropertyChanged(); } }
    }

    public string Comment
    {
        get => comment;
        set { if (comment != value) { comment = value; OnPropertyChanged(); } }
    }

    public string SymbolTable
    {
        get => symbolTable;
        set { if (symbolTable != value) { symbolTable = value; OnPropertyChanged(); } }
    }

    public string SymbolCode
    {
        get => symbolCode;
        set { if (symbolCode != value) { symbolCode = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsTransmitting
    {
        get => isTransmitting;
        private set { if (isTransmitting != value) { isTransmitting = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotTransmitting)); } }
    }

    public bool IsNotTransmitting => !isTransmitting;

    public DesktopCommand TransmitCommand { get; }
    public DesktopCommand RemoveCommand   { get; }
    public DesktopCommand ClearAllCommand { get; }

    private async Task TransmitAsync()
    {
        var callsign = TrackedCallsign.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(callsign))
        {
            StatusText = "Enter the callsign of the station to track.";
            return;
        }

        if (!double.TryParse(LatitudeText, out var lat) || lat < -90 || lat > 90)
        {
            StatusText = "Enter a valid latitude (e.g. 33.7490)";
            return;
        }

        if (!double.TryParse(LongitudeText, out var lon) || lon < -180 || lon > 180)
        {
            StatusText = "Enter a valid longitude (e.g. -84.3880)";
            return;
        }

        var symTable = (SymbolTable.Length > 0 ? SymbolTable[0] : '/');
        var symCode  = (SymbolCode.Length > 0  ? SymbolCode[0]  : '>');

        IsTransmitting = true;
        StatusText     = $"Transmitting position for {callsign}…";

        if (service is null) { StatusText = "Not connected to APRS-IS."; IsTransmitting = false; return; }
        var result = await service.TransmitAsync(
            callsign, lat, lon, symTable, symCode, Comment).ConfigureAwait(false);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsTransmitting = false;
            StatusText = result.IsSuccess
                ? $"✓ {callsign} placed on map. Packet: {result.Packet}"
                : $"✗ Failed: {result.Error}";
            RefreshList();
        });
    }

    private void RemoveSelected()
    {
        if (SelectedStation is null) return;
        service?.Remove(SelectedStation.Callsign);
        StatusText = $"{SelectedStation.Callsign} removed.";
        RefreshList();
    }

    private void ClearAll()
    {
        service?.ClearAll();
        StatusText = "All shadow stations cleared.";
        RefreshList();
    }

    private void RefreshList()
    {
        ActiveStations.Clear();
        foreach (var s in service?.ActiveStations.Values ?? [])
            ActiveStations.Add(new ShadowStationRowViewModel(s));
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ShadowStationRowViewModel
{
    public ShadowStationRowViewModel(Runtime.ShadowStation s)
    {
        Callsign     = s.Callsign;
        PositionText = $"{s.Latitude:F4}°, {s.Longitude:F4}°";
        LastTx       = s.LastTransmittedUtc.ToLocalTime().ToString("HH:mm:ss");
        Comment      = s.Comment ?? string.Empty;
    }

    public string Callsign     { get; }
    public string PositionText { get; }
    public string LastTx       { get; }
    public string Comment      { get; }
}
