using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// No-op RF beacon transmit client used when no RF transmit hardware is configured.
/// The digipeater service requires this but RF transmit is handled by the managed modem
/// or KISS-TCP path rather than through this interface.
/// </summary>
public sealed class NullRfBeaconTransmitClient : IRfBeaconTransmitClient
{
    public Task<BeaconNowResult> SendBeaconAsync(string rawPacket, CancellationToken cancellationToken)
        => Task.FromResult(new BeaconNowResult(
            PacketGenerated:   false,
            TransmitAttempted: false,
            Transmitted:       false,
            Blocked:           true,
            Packet:            null,
            Message:           "No RF transmit configured.",
            TransmitResult:    null,
            ValidationErrors:  Array.Empty<string>()));
}
