namespace Aprs.Services;

public interface IAprsMessageTransmitService
{
    /// <summary>
    /// Sends one already-formatted APRS message packet through a safe transport abstraction.
    /// </summary>
    Task<AprsMessageTransmitResult> SendAsync(string rawPacket, CancellationToken cancellationToken);
}
