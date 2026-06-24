namespace Aprs.Transport;

public interface ISerialPortDiscovery
{
    IReadOnlyList<string> GetAvailablePortNames();
}
