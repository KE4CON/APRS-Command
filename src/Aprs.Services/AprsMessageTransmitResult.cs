namespace Aprs.Services;

public sealed record AprsMessageTransmitResult(
    bool IsSuccess,
    DateTimeOffset TimestampUtc,
    string RawPacket,
    string? FailureReason)
{
    public static AprsMessageTransmitResult Succeeded(DateTimeOffset timestampUtc, string rawPacket)
    {
        return new AprsMessageTransmitResult(true, timestampUtc, rawPacket, FailureReason: null);
    }

    public static AprsMessageTransmitResult Failed(DateTimeOffset timestampUtc, string rawPacket, string failureReason)
    {
        return new AprsMessageTransmitResult(false, timestampUtc, rawPacket, failureReason);
    }
}
