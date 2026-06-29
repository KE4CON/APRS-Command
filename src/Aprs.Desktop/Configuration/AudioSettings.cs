namespace Aprs.Desktop.Configuration;

/// <summary>Persisted audio alert preferences.</summary>
public sealed record AudioSettings(
    int VolumePercent,
    bool PlayOnMessageReceived,
    bool PlayOnWarningAlert,
    bool PlayOnCriticalAlert,
    bool PlayOnConnectionEvents,
    /// <summary>
    /// Optional custom WAV file paths per alert type. Null or empty = use the built-in
    /// synthesised tone. Must be a valid path to a .wav file readable by the OS audio player.
    /// </summary>
    string? CustomSoundMessageReceived,
    string? CustomSoundWarningAlert,
    string? CustomSoundCriticalAlert,
    string? CustomSoundConnected,
    string? CustomSoundDisconnected)
{
    public static AudioSettings Default { get; } = new(
        VolumePercent:                 70,
        PlayOnMessageReceived:         true,
        PlayOnWarningAlert:            true,
        PlayOnCriticalAlert:           true,
        PlayOnConnectionEvents:        false,
        CustomSoundMessageReceived:    null,
        CustomSoundWarningAlert:       null,
        CustomSoundCriticalAlert:      null,
        CustomSoundConnected:          null,
        CustomSoundDisconnected:       null);
}
