namespace Aprs.Services;

public interface IRfBeaconTransmitClient
{
    /// <summary>
    /// Future RF/TNC beacon transmit hook. No production RF implementation exists yet.
    /// </summary>
    Task<BeaconNowResult> SendBeaconAsync(string rawPacket, CancellationToken cancellationToken);
}
