namespace Aprs.Services;

public sealed record BeaconSchedulerConfiguration(
    bool SchedulerEnabled,
    bool AprsIsBeaconEnabled,
    bool RfBeaconEnabled,
    TimeSpan MinimumBeaconInterval,
    string Destination,
    bool RequireTransmitConfirmation,
    SmartBeaconingConfiguration SmartBeaconing)
{
    public static BeaconSchedulerConfiguration Default { get; } = new(
        SchedulerEnabled: false,
        AprsIsBeaconEnabled: false,
        RfBeaconEnabled: false,
        MinimumBeaconInterval: TimeSpan.FromMinutes(5),
        Destination: Aprs.Core.AprsConstants.ToCall,
        RequireTransmitConfirmation: true,
        SmartBeaconing: SmartBeaconingConfiguration.Default);
}
