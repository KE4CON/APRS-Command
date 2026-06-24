namespace Aprs.Services;

public sealed record WeatherBeaconSchedulerState(
    bool SchedulerEnabled,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    string? SelectedWeatherSourceDriverId,
    DateTimeOffset? LastWeatherObservationTimeUtc,
    string? LastWeatherObservationSource,
    string? LastGeneratedWeatherPacket,
    WeatherBeaconTransmitResult? LastTransmitResult,
    string? LastBlockedReason,
    string? LastErrorOrWarning,
    DateTimeOffset? NextScheduledTransmitTimeUtc,
    DateTimeOffset? LastScheduledTransmitTimeUtc,
    int TransmitCount,
    int BlockedTransmitCount);
