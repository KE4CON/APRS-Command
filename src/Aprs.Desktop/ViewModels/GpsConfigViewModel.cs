using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Transport;

namespace Aprs.Desktop.ViewModels;

public sealed class GpsConfigViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private bool enabled;
    private string serialPortName = string.Empty;
    private int baudRate = 4800;
    private bool updateStationPosition;
    private string statusText = string.Empty;

    // GPSD settings
    private bool gpsdEnabled;
    private string gpsdHost = "127.0.0.1";
    private int gpsdPort = 2947;

    public GpsConfigViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        AvailableSerialPorts = new ObservableCollection<string>(
            new SerialPortDiscovery().GetAvailablePortNames());
        AvailableBaudRates = [4800, 9600, 19200, 38400, 57600, 115200];
        SaveCommand   = new DesktopCommand(Save);
        RevertCommand = new DesktopCommand(Load);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AvailableSerialPorts { get; }
    public IReadOnlyList<int> AvailableBaudRates { get; }

    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; OnPropertyChanged(); } }
    }

    public string SerialPortName
    {
        get => serialPortName;
        set { if (serialPortName != value) { serialPortName = value; OnPropertyChanged(); } }
    }

    public int BaudRate
    {
        get => baudRate;
        set { if (baudRate != value) { baudRate = value; OnPropertyChanged(); } }
    }

    public bool UpdateStationPosition
    {
        get => updateStationPosition;
        set { if (updateStationPosition != value) { updateStationPosition = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool GpsdEnabled
    {
        get => gpsdEnabled;
        set { if (gpsdEnabled != value) { gpsdEnabled = value; OnPropertyChanged(); } }
    }

    public string GpsdHost
    {
        get => gpsdHost;
        set { if (gpsdHost != value) { gpsdHost = value; OnPropertyChanged(); } }
    }

    public int GpsdPort
    {
        get => gpsdPort;
        set { if (gpsdPort != value) { gpsdPort = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand { get; }
    public DesktopCommand RevertCommand { get; }

    public void Load()
    {
        var settings = store.Load();
        var s = settings.Gps;
        var g = settings.Gpsd;
        GpsdEnabled = g.Enabled;
        GpsdHost    = g.Host;
        GpsdPort    = g.Port;
        Enabled               = s.Enabled;
        SerialPortName        = s.SerialPortName;
        BaudRate              = s.BaudRate;
        UpdateStationPosition = s.UpdateStationPosition;
        StatusText = "Loaded.";
    }

    public void Save()
    {
        var settings = new GpsSettings(
            Enabled:               Enabled,
            SerialPortName:        SerialPortName.Trim(),
            BaudRate:              BaudRate,
            UpdateStationPosition: UpdateStationPosition);
        var gpsdSettings = new Aprs.Desktop.Configuration.GpsdSettings(
            Enabled: GpsdEnabled,
            Host:    GpsdHost.Trim(),
            Port:    GpsdPort);
        store.Update(s => s with { Gps = settings, Gpsd = gpsdSettings });
        StatusText = Enabled
            ? $"GPS enabled on {SerialPortName} at {BaudRate} baud. Restart the app to apply."
            : "GPS disabled.";
    }

    public static GpsConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
