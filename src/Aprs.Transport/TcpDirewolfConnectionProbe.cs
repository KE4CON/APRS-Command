using System.Net.Sockets;

namespace Aprs.Transport;

public sealed class TcpDirewolfConnectionProbe : IDirewolfConnectionProbe
{
    public async Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, linkedCancellation.Token).ConfigureAwait(false);
    }
}
