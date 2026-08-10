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

    /// <summary>
    /// Optional global transmit-inhibit gate. When set and inhibited (for example exercise mode),
    /// RF transmit is blocked before any AX.25 frame reaches a KISS port.
    /// </summary>
    public ITransmitInhibitGate? InhibitGate { get; set; }

    public async Task<BeaconNowResult> SendBeaconAsync(
        string rawPacket, CancellationToken cancellationToken)
    {
        // Global inhibit (exercise/training mode) hard-blocks every RF transmit path.
        var gate = InhibitGate;
        if (gate is not null && gate.IsTransmitInhibited)
            return Fail(gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).", rawPacket);

        if (string.IsNullOrWhiteSpace(rawPacket))
            return Fail("Empty packet.", rawPacket);

        // Identity gate at the single RF chokepoint: never key up on the air with a placeholder callsign.
        // Unlike APRS-IS (which is blocked by the missing passcode), RF has no passcode, so N0CALL/empty
        // would otherwise transmit unidentified — an FCC §97.119 identification violation. This covers every
        // RF path that fans out through here (beacon, message, weather). See CLAUDE.md transmit-safety.
        var source = ExtractSourceCallsign(rawPacket);
        if (IsPlaceholderCallsign(source))
            return Fail(
                $"RF transmit blocked: '{(source.Length == 0 ? "(empty)" : source)}' is not a valid station callsign. " +
                "Set your callsign in Station Setup before transmitting on RF.", rawPacket);

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

    /// <summary>Returns the source callsign (with SSID) from a TNC2 line, or "" if none.</summary>
    private static string ExtractSourceCallsign(string rawPacket)
    {
        var gt = rawPacket.IndexOf('>');
        return gt > 0 ? rawPacket[..gt].Trim() : string.Empty;
    }

    /// <summary>True for an empty or placeholder callsign that must never key up a real transmitter.</summary>
    private static bool IsPlaceholderCallsign(string callsignWithSsid)
    {
        if (string.IsNullOrWhiteSpace(callsignWithSsid)) return true;
        var dash = callsignWithSsid.IndexOf('-');
        var baseCall = dash > 0 ? callsignWithSsid[..dash] : callsignWithSsid;
        return string.Equals(baseCall, "N0CALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(baseCall, "NOCALL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(baseCall, "MYCALL", StringComparison.OrdinalIgnoreCase);
    }
}
