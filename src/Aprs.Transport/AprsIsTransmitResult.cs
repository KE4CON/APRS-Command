namespace Aprs.Transport;

public sealed record AprsIsTransmitResult(
    DateTimeOffset TimestampUtc,
    string RawPacket,
    AprsTransmitDestinationTransport DestinationTransport,
    bool IsSuccess,
    string? FailureReason,
    AprsIsConnectionState ConnectedStateAtRequest)
{
    public static AprsIsTransmitResult Succeeded(
        DateTimeOffset timestampUtc,
        string rawPacket,
        AprsIsConnectionState connectedStateAtRequest)
    {
        return new AprsIsTransmitResult(
            timestampUtc,
            rawPacket,
            AprsTransmitDestinationTransport.AprsIs,
            IsSuccess: true,
            FailureReason: null,
            connectedStateAtRequest);
    }

    public static AprsIsTransmitResult Failed(
        DateTimeOffset timestampUtc,
        string rawPacket,
        AprsIsConnectionState connectedStateAtRequest,
        string failureReason)
    {
        return new AprsIsTransmitResult(
            timestampUtc,
            rawPacket,
            AprsTransmitDestinationTransport.AprsIs,
            IsSuccess: false,
            FailureReason: failureReason,
            connectedStateAtRequest);
    }
}
