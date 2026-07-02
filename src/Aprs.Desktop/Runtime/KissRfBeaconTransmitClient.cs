using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// IRfBeaconTransmitClient that fans out beacon transmit to all enabled
/// KISS connections — Serial KISS (hardware TNC) and KISS-TCP (GrayWolf,
/// Direwolf standalone). Replaces NullRfBeaconTransmitClient in production.
///
/// The GetTcpClients and GetSerialClients delegates are set after construction
/// (once coordinators exist) to avoid a circular DI dependency.
/// </summary>
public sealed class KissRfBeaconTransmitClient : IRfBeaconTransmitClient
{
    public Func<IReadOnlyList<TcpKissClient>>    GetTcpClients    { get; set; }
        = static () => Array.Empty<TcpKissClient>();
    public Func<IReadOnlyList<SerialKissClient>> GetSerialClients { get; set; }
        = static () => Array.Empty<SerialKissClient>();

    public async Task<BeaconNowResult> SendBeaconAsync(
        string rawPacket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPacket))
            return Fail("Empty packet.", rawPacket);

        var ax25 = Ax25AprsFrameEncoder.Encode(rawPacket);
        if (ax25 is null || ax25.Length == 0)
            return Fail($"Could not encode to AX.25: '{rawPacket}'", rawPacket);

        bool transmitted = false;
        var  errors      = new List<string>();

        foreach (var client in GetTcpClients())
        {
            try
            {
                var r = await client.SendFrameAsync(
                    portNumber: 0, commandType: KissCommandType.DataFrame,
                    ax25Payload: ax25, transmitConfirmed: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (r.IsSuccess) transmitted = true;
                else errors.Add($"KISS-TCP {client.Configuration.Host}:{client.Configuration.Port}: {r.FailureReason}");
            }
            catch (Exception ex) { errors.Add($"KISS-TCP exception: {ex.Message}"); }
        }

        foreach (var client in GetSerialClients())
        {
            try
            {
                var r = await client.SendFrameAsync(
                    portNumber: 0, commandType: KissCommandType.DataFrame,
                    ax25Payload: ax25, transmitConfirmed: false,
                    rfSafetyEnabled: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (r.IsSuccess) transmitted = true;
                else errors.Add($"Serial KISS {client.Configuration.PortName}: {r.FailureReason}");
            }
            catch (Exception ex) { errors.Add($"Serial KISS exception: {ex.Message}"); }
        }

        if (!transmitted && errors.Count == 0)
            return new BeaconNowResult(true, false, false, false, rawPacket,
                "No RF transmit ports connected.", null, Array.Empty<string>());

        return new BeaconNowResult(true, true, transmitted, false, rawPacket,
            transmitted ? "Transmitted on RF."
                        : $"RF transmit failed: {string.Join("; ", errors)}",
            null,
            errors.Count > 0 ? errors.ToArray() : Array.Empty<string>());
    }

    private static BeaconNowResult Fail(string msg, string? pkt)
        => new(false, false, false, true, pkt, msg, null, new[] { msg });
}
