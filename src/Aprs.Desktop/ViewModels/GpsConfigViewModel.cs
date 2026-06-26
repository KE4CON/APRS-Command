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

    public DesktopCommand SaveCommand { get; }
    public DesktopCommand RevertCommand { get; }

    public void Load()
    {
        var s = store.Load().Gps;
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
        store.Update(s => s with { Gps = settings });
        StatusText = Enabled
            ? $"GPS enabled on {SerialPortName} at {BaudRate} baud. Restart the app to apply."
            : "GPS disabled.";
    }

    public static GpsConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
