namespace Aprs.Transport;

public interface IAgwpePortProvider
{
    IReadOnlyList<AgwpePortDefinition> GetKnownPorts();
}
