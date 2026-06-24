namespace Aprs.Services;

public sealed record WeatherBeaconTransmitResult(
    DateTimeOffset TimestampUtc,
    WeatherBeaconTransmitTransport DestinationTransport,
    string? RawPacket,
    bool IsSuccess,
    string? FailureReason)
{
    public static WeatherBeaconTransmitResult Succeeded(
        DateTimeOffset timestampUtc,
        WeatherBeaconTransmitTransport destinationTransport,
        string rawPacket)
    {
        return new WeatherBeaconTransmitResult(
            timestampUtc,
            destinationTransport,
            rawPacket,
            IsSuccess: true,
            FailureReason: null);
    }

    public static WeatherBeaconTransmitResult Failed(
        DateTimeOffset timestampUtc,
        WeatherBeaconTransmitTransport destinationTransport,
        string? rawPacket,
        string failureReason)
    {
        return new WeatherBeaconTransmitResult(
            timestampUtc,
            destinationTransport,
            rawPacket,
            IsSuccess: false,
            FailureReason: failureReason);
    }
}
