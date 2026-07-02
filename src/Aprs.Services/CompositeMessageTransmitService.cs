using Aprs.Transport;

namespace Aprs.Services;

/// <summary>
/// IAprsMessageTransmitService that fans out to both APRS-IS and an optional RF path.
/// Used when the operator has both internet connectivity and a TNC/radio configured.
///
/// Both paths are attempted. Success is reported if either path succeeds.
/// If APRS-IS is unavailable (not connected), only RF is used, and vice versa.
/// </summary>
public sealed class CompositeMessageTransmitService : IAprsMessageTransmitService
{
    private readonly IAprsIsClient? aprsIsClient;
    private readonly IRfBeaconTransmitClient? rfClient;
    private readonly bool transmitConfirmed;

    public CompositeMessageTransmitService(
        IAprsIsClient? aprsIsClient,
        IRfBeaconTransmitClient? rfClient,
        bool transmitConfirmed = true)
    {
        this.aprsIsClient    = aprsIsClient;
        this.rfClient        = rfClient;
        this.transmitConfirmed = transmitConfirmed;
    }

    public async Task<AprsMessageTransmitResult> SendAsync(
        string rawPacket,
        CancellationToken cancellationToken)
    {
        AprsMessageTransmitResult? aprsIsResult = null;
        AprsMessageTransmitResult? rfResult     = null;

        // APRS-IS path
        if (aprsIsClient is not null
         && aprsIsClient.State == AprsIsConnectionState.Connected)
        {
            var r = await aprsIsClient.SendRawPacketAsync(
                rawPacket, transmitConfirmed, cancellationToken).ConfigureAwait(false);
            aprsIsResult = r.IsSuccess
                ? AprsMessageTransmitResult.Succeeded(r.TimestampUtc, r.RawPacket)
                : AprsMessageTransmitResult.Failed(r.TimestampUtc, r.RawPacket,
                    r.FailureReason ?? "APRS-IS transmit failed.");
        }

        // RF path
        if (rfClient is not null)
        {
            var r = await rfClient.SendBeaconAsync(rawPacket, cancellationToken)
                                  .ConfigureAwait(false);
            rfResult = r.Transmitted
                ? AprsMessageTransmitResult.Succeeded(DateTimeOffset.UtcNow, rawPacket)
                : AprsMessageTransmitResult.Failed(DateTimeOffset.UtcNow, rawPacket,
                    r.Message ?? "RF transmit failed.");
        }

        // Return the best result — success on either path counts as success
        if (aprsIsResult?.IsSuccess == true) return aprsIsResult;
        if (rfResult?.IsSuccess     == true) return rfResult;
        if (aprsIsResult is not null)         return aprsIsResult;
        if (rfResult is not null)             return rfResult;

        return AprsMessageTransmitResult.Failed(DateTimeOffset.UtcNow, rawPacket,
            "No transmit path available (APRS-IS not connected, no RF client configured).");
    }
}
