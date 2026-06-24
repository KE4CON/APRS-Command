namespace Aprs.Transport;

public sealed class ConfiguredAgwpePortProvider : IAgwpePortProvider
{
    private readonly AgwpeConfiguration configuration;

    public ConfiguredAgwpePortProvider(AgwpeConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public IReadOnlyList<AgwpePortDefinition> GetKnownPorts()
    {
        return
        [
            new AgwpePortDefinition(
                configuration.SelectedRadioPort,
                $"AGWPE port {configuration.SelectedRadioPort}",
                configuration.ReceiveEnabled,
                configuration.TransmitEnabled)
        ];
    }
}
