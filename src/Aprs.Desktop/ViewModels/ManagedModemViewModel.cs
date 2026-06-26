using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Audio;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Transport;

namespace Aprs.Desktop.ViewModels;

public sealed class ManagedModemViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private bool enabled;
    private string audioInputDevice = string.Empty;
    private string audioOutputDevice = string.Empty;
    private PttMethod pttMethod;
    private string pttSerialPort = string.Empty;
    private string direwolfPath = string.Empty;
    private int kissPort = 8001;
    private bool transmitEnabled;
    private string statusText = string.Empty;
    private string direwolfFoundText = string.Empty;

    public ManagedModemViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));

        AudioInputDevices  = new ObservableCollection<string>(
            AudioDeviceDiscovery.GetInputDevices().Select(d => d.Name));
        AudioOutputDevices = new ObservableCollection<string>(
            AudioDeviceDiscovery.GetOutputDevices().Select(d => d.Name));
        AvailableSerialPorts = new ObservableCollection<string>(
            new SerialPortDiscovery().GetAvailablePortNames());

        SaveCommand    = new DesktopCommand(Save);
        RevertCommand  = new DesktopCommand(Load);
        RefreshCommand = new DesktopCommand(RefreshDevices);
        Load();
        RefreshDirewolfStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AudioInputDevices  { get; }
    public ObservableCollection<string> AudioOutputDevices { get; }
    public ObservableCollection<string> AvailableSerialPorts { get; }

    public IReadOnlyList<PttMethod> AvailablePttMethods { get; } =
        [PttMethod.None, PttMethod.Rts, PttMethod.Dtr];

    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; OnPropertyChanged(); } }
    }

    public string AudioInputDevice
    {
        get => audioInputDevice;
        set { if (audioInputDevice != value) { audioInputDevice = value; OnPropertyChanged(); } }
    }

    public string AudioOutputDevice
    {
        get => audioOutputDevice;
        set { if (audioOutputDevice != value) { audioOutputDevice = value; OnPropertyChanged(); } }
    }

    public PttMethod PttMethod
    {
        get => pttMethod;
        set { if (pttMethod != value) { pttMethod = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowSerialPortPicker)); } }
    }

    public bool ShowSerialPortPicker => PttMethod is PttMethod.Rts or PttMethod.Dtr;

    public string PttSerialPort
    {
        get => pttSerialPort;
        set { if (pttSerialPort != value) { pttSerialPort = value; OnPropertyChanged(); } }
    }

    public string DirewolfPath
    {
        get => direwolfPath;
        set { if (direwolfPath != value) { direwolfPath = value; OnPropertyChanged(); RefreshDirewolfStatus(); } }
    }

    public int KissPort
    {
        get => kissPort;
        set { if (kissPort != value) { kissPort = value; OnPropertyChanged(); } }
    }

    public bool TransmitEnabled
    {
        get => transmitEnabled;
        set { if (transmitEnabled != value) { transmitEnabled = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public string DirewolfFoundText
    {
        get => direwolfFoundText;
        private set { if (direwolfFoundText != value) { direwolfFoundText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand    { get; }
    public DesktopCommand RevertCommand  { get; }
    public DesktopCommand RefreshCommand { get; }

    public void Load()
    {
        var s = store.Load().ManagedModem;
        Enabled            = s.Enabled;
        AudioInputDevice   = s.AudioInputDevice;
        AudioOutputDevice  = s.AudioOutputDevice;
        PttMethod          = s.PttMethod;
        PttSerialPort      = s.PttSerialPort;
        DirewolfPath       = s.DirewolfPath;
        KissPort           = s.KissPort;
        TransmitEnabled    = s.TransmitEnabled;
        StatusText         = "Loaded.";
    }

    public void Save()
    {
        var settings = new ManagedModemSettings(
            Enabled:           Enabled,
            AudioInputDevice:  AudioInputDevice.Trim(),
            AudioOutputDevice: AudioOutputDevice.Trim(),
            PttMethod:         PttMethod,
            PttSerialPort:     PttSerialPort.Trim(),
            CallsignSsid:      store.Load().Station.Ssid,
            DirewolfPath:      DirewolfPath.Trim(),
            KissPort:          KissPort,
            TransmitEnabled:   TransmitEnabled);

        store.Update(s => s with { ManagedModem = settings });
        StatusText = Enabled
            ? "Saved. Restart APRS Command to start the managed modem."
            : "Saved. Managed modem disabled.";
    }

    private void RefreshDevices()
    {
        AudioInputDevices.Clear();
        foreach (var d in AudioDeviceDiscovery.GetInputDevices())
            AudioInputDevices.Add(d.Name);

        AudioOutputDevices.Clear();
        foreach (var d in AudioDeviceDiscovery.GetOutputDevices())
            AudioOutputDevices.Add(d.Name);

        AvailableSerialPorts.Clear();
        foreach (var p in new SerialPortDiscovery().GetAvailablePortNames())
            AvailableSerialPorts.Add(p);

        RefreshDirewolfStatus();
        StatusText = "Devices refreshed.";
    }

    private void RefreshDirewolfStatus()
    {
        var path = DirewolfProcessManager.FindDirewolf(
            string.IsNullOrWhiteSpace(DirewolfPath) ? null : DirewolfPath);
        DirewolfFoundText = path is not null
            ? $"✓ Direwolf found: {path}"
            : "✗ Direwolf not found. Install it or set the path below.";
    }

    public static ManagedModemViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
