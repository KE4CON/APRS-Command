namespace Aprs.Services;

public sealed record StationProfileValidationOptions(
    bool AprsIsTransmitConfigured,
    bool RfTransmitConfigured)
{
    public static StationProfileValidationOptions Default { get; } = new(
        AprsIsTransmitConfigured: false,
        RfTransmitConfigured: false);
}
