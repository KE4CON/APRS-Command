namespace Aprs.Transport;

public sealed record SerialKissTransmitResult(
    DateTimeOffset TimestampUtc,
    AprsTransmitDestinationTransport DestinationTransport,
    bool IsSuccess,
    string? FailureReason,
    SerialKissConnectionState ConnectedStateAtRequest,
    KissFrame? Frame)
{
    public static SerialKissTransmitResult Succeeded(DateTimeOffset timestampUtc, SerialKissConnectionState state, KissFrame frame)
    {
        return new SerialKissTransmitResult(timestampUtc, AprsTransmitDestinationTransport.SerialKiss, true, null, state, frame);
    }

    public static SerialKissTransmitResult Failed(DateTimeOffset timestampUtc, SerialKissConnectionState state, string failureReason, KissFrame? frame = null)
    {
        return new SerialKissTransmitResult(timestampUtc, AprsTransmitDestinationTransport.SerialKiss, false, failureReason, state, frame);
    }
}
