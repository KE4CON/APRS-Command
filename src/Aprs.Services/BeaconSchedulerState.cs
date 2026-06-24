using Aprs.Transport;

namespace Aprs.Services;

public sealed record BeaconSchedulerState(
    bool SchedulerEnabled,
    bool AprsIsBeaconEnabled,
    bool RfBeaconEnabled,
    DateTimeOffset? NextAprsIsBeaconTimeUtc,
    DateTimeOffset? NextRfBeaconTimeUtc,
    DateTimeOffset? LastAprsIsBeaconTimeUtc,
    DateTimeOffset? LastRfBeaconTimeUtc,
    string? LastGeneratedBeaconPacket,
    AprsIsTransmitResult? LastTransmitResult,
    string? LastErrorOrWarning,
    LocalStationProfile CurrentStationProfile);
