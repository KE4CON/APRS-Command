namespace Aprs.Transport;

public sealed record TcpKissTransmitResult(
    DateTimeOffset TimestampUtc,
    AprsTransmitDestinationTransport DestinationTransport,
    bool IsSuccess,
    string? FailureReason,
    TcpKissConnectionState ConnectedStateAtRequest,
    KissFrame? Frame)
{
    public static TcpKissTransmitResult Succeeded(DateTimeOffset timestampUtc, TcpKissConnectionState state, KissFrame frame)
    {
        return new TcpKissTransmitResult(timestampUtc, AprsTransmitDestinationTransport.TcpKiss, true, null, state, frame);
    }

    public static TcpKissTransmitResult Failed(DateTimeOffset timestampUtc, TcpKissConnectionState state, string failureReason, KissFrame? frame = null)
    {
        return new TcpKissTransmitResult(timestampUtc, AprsTransmitDestinationTransport.TcpKiss, false, failureReason, state, frame);
    }
}
