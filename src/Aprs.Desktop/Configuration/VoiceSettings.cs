namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted voice readout preferences.
/// The master switch (Enabled) must be on for any speech to occur.
/// Individual toggles allow fine-grained control over which events are spoken.
/// </summary>
public sealed record VoiceSettings(
    bool Enabled,
    bool SpeakIncomingMessages,
    bool SpeakNetCheckIns,
    bool SpeakWeatherAlerts,
    bool SpeakStationAlerts,
    bool SpeakConnectionEvents,
    bool SpeakBeaconConfirmations)
{
    public static VoiceSettings Default { get; } = new(
        Enabled:                  false,
        SpeakIncomingMessages:    true,
        SpeakNetCheckIns:         true,
        SpeakWeatherAlerts:       true,
        SpeakStationAlerts:       false,
        SpeakConnectionEvents:    false,
        SpeakBeaconConfirmations: false);
}
