namespace Aprs.Services;

public sealed record WeatherBeaconConfiguration(
    bool WeatherBeaconEnabled,
    bool AprsIsWeatherTransmitEnabled,
    bool RfWeatherTransmitEnabled,
    string? SelectedWeatherSourceDriverId,
    TimeSpan WeatherTransmitInterval,
    TimeSpan MinimumAllowedTransmitInterval,
    bool RequireConfirmationBeforeTransmit,
    bool RejectStaleData,
    TimeSpan StaleDataThreshold,
    bool IncludePosition,
    bool UseLocalStationProfilePositionWhenMissing,
    string AprsDestination,
    IReadOnlyList<string> RfPath,
    string? CommentText,
    DateTimeOffset? LastTransmitTimestampUtc,
    DateTimeOffset? NextTransmitTimestampUtc,
    DateTimeOffset CreatedTimestampUtc,
    DateTimeOffset UpdatedTimestampUtc)
{
    public static WeatherBeaconConfiguration Default { get; } = new(
        WeatherBeaconEnabled: false,
        AprsIsWeatherTransmitEnabled: false,
        RfWeatherTransmitEnabled: false,
        SelectedWeatherSourceDriverId: null,
        WeatherTransmitInterval: TimeSpan.FromMinutes(30),
        MinimumAllowedTransmitInterval: TimeSpan.FromMinutes(5),
        RequireConfirmationBeforeTransmit: true,
        RejectStaleData: true,
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        IncludePosition: true,
        UseLocalStationProfilePositionWhenMissing: true,
        AprsDestination: "APRS",
        RfPath: [],
        CommentText: null,
        LastTransmitTimestampUtc: null,
        NextTransmitTimestampUtc: null,
        CreatedTimestampUtc: DateTimeOffset.MinValue,
        UpdatedTimestampUtc: DateTimeOffset.MinValue);
}
