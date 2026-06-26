namespace Aprs.Desktop.Configuration;

/// <summary>Persisted audio alert preferences.</summary>
public sealed record AudioSettings(
    int VolumePercent,
    bool PlayOnMessageReceived,
    bool PlayOnWarningAlert,
    bool PlayOnCriticalAlert,
    bool PlayOnConnectionEvents)
{
    public static AudioSettings Default { get; } = new(
        VolumePercent:          70,
        PlayOnMessageReceived:  true,
        PlayOnWarningAlert:     true,
        PlayOnCriticalAlert:    true,
        PlayOnConnectionEvents: false);
}
