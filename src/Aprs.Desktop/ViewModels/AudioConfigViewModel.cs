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
    private bool voiceEnabled;
    private bool voiceSpeakMessages;
    private bool voiceSpeakNetCheckIns;
    private bool voiceSpeakWeatherAlerts;
    private bool voiceSpeakStationAlerts;
    private bool voiceSpeakConnectionEvents;
    private bool voiceSpeakBeaconConfirmations;
    private string statusText = string.Empty;
    private string? customSoundMessageReceived;
    private string? customSoundWarningAlert;
    private string? customSoundCriticalAlert;
    private string? customSoundConnected;
    private string? customSoundDisconnected;

    public AudioConfigViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        SaveCommand                  = new DesktopCommand(Save);
        RevertCommand                = new DesktopCommand(Load);
        BrowseMessageSoundCommand    = new DesktopCommand(async () => CustomSoundMessageReceived  = await BrowseWavFileAsync() ?? CustomSoundMessageReceived);
        BrowseWarningSoundCommand    = new DesktopCommand(async () => CustomSoundWarningAlert     = await BrowseWavFileAsync() ?? CustomSoundWarningAlert);
        BrowseCriticalSoundCommand   = new DesktopCommand(async () => CustomSoundCriticalAlert    = await BrowseWavFileAsync() ?? CustomSoundCriticalAlert);
        BrowseConnectedSoundCommand  = new DesktopCommand(async () => CustomSoundConnected        = await BrowseWavFileAsync() ?? CustomSoundConnected);
        BrowseDisconnectedSoundCommand = new DesktopCommand(async () => CustomSoundDisconnected   = await BrowseWavFileAsync() ?? CustomSoundDisconnected);
        TestSoundCommand             = new DesktopCommand(() => new Audio.SoundAlertService(store).Play(Audio.AlertSound.MessageReceived));
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

    public bool VoiceEnabled
    {
        get => voiceEnabled;
        set { voiceEnabled = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakMessages
    {
        get => voiceSpeakMessages;
        set { voiceSpeakMessages = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakNetCheckIns
    {
        get => voiceSpeakNetCheckIns;
        set { voiceSpeakNetCheckIns = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakWeatherAlerts
    {
        get => voiceSpeakWeatherAlerts;
        set { voiceSpeakWeatherAlerts = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakStationAlerts
    {
        get => voiceSpeakStationAlerts;
        set { voiceSpeakStationAlerts = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakConnectionEvents
    {
        get => voiceSpeakConnectionEvents;
        set { voiceSpeakConnectionEvents = value; OnPropertyChanged(); }
    }
    public bool VoiceSpeakBeaconConfirmations
    {
        get => voiceSpeakBeaconConfirmations;
        set { voiceSpeakBeaconConfirmations = value; OnPropertyChanged(); }
    }

    public string? CustomSoundMessageReceived
    {
        get => customSoundMessageReceived;
        set { if (customSoundMessageReceived != value) { customSoundMessageReceived = value; OnPropertyChanged(); } }
    }

    public string? CustomSoundWarningAlert
    {
        get => customSoundWarningAlert;
        set { if (customSoundWarningAlert != value) { customSoundWarningAlert = value; OnPropertyChanged(); } }
    }

    public string? CustomSoundCriticalAlert
    {
        get => customSoundCriticalAlert;
        set { if (customSoundCriticalAlert != value) { customSoundCriticalAlert = value; OnPropertyChanged(); } }
    }

    public string? CustomSoundConnected
    {
        get => customSoundConnected;
        set { if (customSoundConnected != value) { customSoundConnected = value; OnPropertyChanged(); } }
    }

    public string? CustomSoundDisconnected
    {
        get => customSoundDisconnected;
        set { if (customSoundDisconnected != value) { customSoundDisconnected = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand { get; }
    public DesktopCommand RevertCommand { get; }
    public DesktopCommand BrowseMessageSoundCommand { get; }
    public DesktopCommand BrowseWarningSoundCommand { get; }
    public DesktopCommand BrowseCriticalSoundCommand { get; }
    public DesktopCommand BrowseConnectedSoundCommand { get; }
    public DesktopCommand BrowseDisconnectedSoundCommand { get; }
    public DesktopCommand TestSoundCommand { get; }

    public void Load()
    {
        var all = store.Load();
        var s   = all.Audio;
        VolumePercent          = s.VolumePercent;
        PlayOnMessageReceived  = s.PlayOnMessageReceived;
        PlayOnWarningAlert     = s.PlayOnWarningAlert;
        PlayOnCriticalAlert    = s.PlayOnCriticalAlert;
        PlayOnConnectionEvents       = s.PlayOnConnectionEvents;
        CustomSoundMessageReceived   = s.CustomSoundMessageReceived;
        CustomSoundWarningAlert      = s.CustomSoundWarningAlert;
        CustomSoundCriticalAlert     = s.CustomSoundCriticalAlert;
        CustomSoundConnected         = s.CustomSoundConnected;
        CustomSoundDisconnected      = s.CustomSoundDisconnected;
        var v = all.Voice;
        VoiceEnabled                  = v.Enabled;
        VoiceSpeakMessages            = v.SpeakIncomingMessages;
        VoiceSpeakNetCheckIns         = v.SpeakNetCheckIns;
        VoiceSpeakWeatherAlerts       = v.SpeakWeatherAlerts;
        VoiceSpeakStationAlerts       = v.SpeakStationAlerts;
        VoiceSpeakConnectionEvents    = v.SpeakConnectionEvents;
        VoiceSpeakBeaconConfirmations = v.SpeakBeaconConfirmations;
        StatusText = "Loaded.";
    }

    public void Save()
    {
        var audio = new AudioSettings(
            VolumePercent:                 VolumePercent,
            PlayOnMessageReceived:         PlayOnMessageReceived,
            PlayOnWarningAlert:            PlayOnWarningAlert,
            PlayOnCriticalAlert:           PlayOnCriticalAlert,
            PlayOnConnectionEvents:        PlayOnConnectionEvents,
            CustomSoundMessageReceived:    NullIfEmpty(CustomSoundMessageReceived),
            CustomSoundWarningAlert:       NullIfEmpty(CustomSoundWarningAlert),
            CustomSoundCriticalAlert:      NullIfEmpty(CustomSoundCriticalAlert),
            CustomSoundConnected:          NullIfEmpty(CustomSoundConnected),
            CustomSoundDisconnected:       NullIfEmpty(CustomSoundDisconnected));
        var voice = new VoiceSettings(
            Enabled:                  VoiceEnabled,
            SpeakIncomingMessages:    VoiceSpeakMessages,
            SpeakNetCheckIns:         VoiceSpeakNetCheckIns,
            SpeakWeatherAlerts:       VoiceSpeakWeatherAlerts,
            SpeakStationAlerts:       VoiceSpeakStationAlerts,
            SpeakConnectionEvents:    VoiceSpeakConnectionEvents,
            SpeakBeaconConfirmations: VoiceSpeakBeaconConfirmations);
        store.Update(s => s with { Audio = audio, Voice = voice });
        StatusText = "Saved.";
    }

    public static AudioConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<string?> BrowseWavFileAsync()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desk
                ? desk.MainWindow
                : null;
            if (topLevel is null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select WAV sound file",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new Avalonia.Platform.Storage.FilePickerFileType("WAV audio files")
                        {
                            Patterns = ["*.wav"]
                        }
                    ]
                });

            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }
        catch { return null; }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
