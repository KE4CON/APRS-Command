namespace Aprs.Transport;

public sealed record AgwpeTransmitResult(
    DateTimeOffset TimestampUtc,
    AprsTransmitDestinationTransport DestinationTransport,
    bool IsSuccess,
    string? FailureReason,
    AgwpeConnectionState ConnectedStateAtRequest,
    AgwpeFrame? Frame)
{
    public static AgwpeTransmitResult Succeeded(DateTimeOffset timestampUtc, AgwpeConnectionState state, AgwpeFrame frame)
    {
        return new AgwpeTransmitResult(timestampUtc, AprsTransmitDestinationTransport.Agwpe, true, null, state, frame);
    }

    public static AgwpeTransmitResult Failed(DateTimeOffset timestampUtc, AgwpeConnectionState state, string failureReason, AgwpeFrame? frame = null)
    {
        return new AgwpeTransmitResult(timestampUtc, AprsTransmitDestinationTransport.Agwpe, false, failureReason, state, frame);
    }
}
