namespace Aprs.Transport;

public interface IDirewolfConnectionProbe
{
    Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken);
}
