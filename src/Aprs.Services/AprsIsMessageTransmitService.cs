using Aprs.Transport;

namespace Aprs.Services;

public sealed class AprsIsMessageTransmitService : IAprsMessageTransmitService
{
    private readonly IAprsIsClient aprsIsClient;
    private readonly bool transmitConfirmed;

    public AprsIsMessageTransmitService(IAprsIsClient aprsIsClient, bool transmitConfirmed = true)
    {
        this.aprsIsClient = aprsIsClient;
        this.transmitConfirmed = transmitConfirmed;
    }

    public async Task<AprsMessageTransmitResult> SendAsync(string rawPacket, CancellationToken cancellationToken)
    {
        var result = await aprsIsClient.SendRawPacketAsync(rawPacket, transmitConfirmed, cancellationToken);
        return result.IsSuccess
            ? AprsMessageTransmitResult.Succeeded(result.TimestampUtc, result.RawPacket)
            : AprsMessageTransmitResult.Failed(
                result.TimestampUtc,
                result.RawPacket,
                result.FailureReason ?? "APRS-IS transmit failed.");
    }
}
