namespace Aprs.Transport;

/// <summary>
/// Production implementation of <see cref="ISerialPortConnectionFactory"/> that creates
/// <see cref="SystemSerialPortConnection"/> instances backed by System.IO.Ports.SerialPort.
/// </summary>
public sealed class SystemSerialPortConnectionFactory : ISerialPortConnectionFactory
{
    public static SystemSerialPortConnectionFactory Instance { get; } = new();

    public ISerialPortConnection Create(SerialKissConfiguration configuration)
        => new SystemSerialPortConnection(configuration);
}
