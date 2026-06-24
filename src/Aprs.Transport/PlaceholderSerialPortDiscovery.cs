namespace Aprs.Transport;

public sealed class PlaceholderSerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<string> GetAvailablePortNames()
    {
        return [];
    }
}
