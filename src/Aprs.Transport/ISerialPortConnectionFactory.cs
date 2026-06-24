namespace Aprs.Transport;

public interface ISerialPortConnectionFactory
{
    ISerialPortConnection Create(SerialKissConfiguration configuration);
}
