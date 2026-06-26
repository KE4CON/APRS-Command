using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

public sealed class AudioConfigViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private int volumePercent;
    private bool playOnMessageReceived;
    private bool playOnWarningAlert;
    private bool playOnCriticalAlert;
    private bool playOnConnectionEvents;
    private string statusText = string.Empty;

    public AudioConfigViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        SaveCommand   = new DesktopCommand(Save);
        RevertCommand = new DesktopCommand(Load);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int VolumePercent
    {
        get => volumePercent;
        set { if (volumePercent != value) { volumePercent = Math.Clamp(value, 0, 100); OnPropertyChanged(); } }
    }

    public bool PlayOnMessageReceived
    {
        get => playOnMessageReceived;
        set { if (playOnMessageReceived != value) { playOnMessageReceived = value; OnPropertyChanged(); } }
    }

    public bool PlayOnWarningAlert
    {
        get => playOnWarningAlert;
        set { if (playOnWarningAlert != value) { playOnWarningAlert = value; OnPropertyChanged(); } }
    }

    public bool PlayOnCriticalAlert
    {
        get => playOnCriticalAlert;
        set { if (playOnCriticalAlert != value) { playOnCriticalAlert = value; OnPropertyChanged(); } }
    }

    public bool PlayOnConnectionEvents
    {
        get => playOnConnectionEvents;
        set { if (playOnConnectionEvents != value) { playOnConnectionEvents = value; OnPropertyChanged(); } }
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
        var s = store.Load().Audio;
        VolumePercent          = s.VolumePercent;
        PlayOnMessageReceived  = s.PlayOnMessageReceived;
        PlayOnWarningAlert     = s.PlayOnWarningAlert;
        PlayOnCriticalAlert    = s.PlayOnCriticalAlert;
        PlayOnConnectionEvents = s.PlayOnConnectionEvents;
        StatusText = "Loaded.";
    }

    public void Save()
    {
        var settings = new AudioSettings(
            VolumePercent:          VolumePercent,
            PlayOnMessageReceived:  PlayOnMessageReceived,
            PlayOnWarningAlert:     PlayOnWarningAlert,
            PlayOnCriticalAlert:    PlayOnCriticalAlert,
            PlayOnConnectionEvents: PlayOnConnectionEvents);
        store.Update(s => s with { Audio = settings });
        StatusText = "Saved.";
    }

    public static AudioConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
