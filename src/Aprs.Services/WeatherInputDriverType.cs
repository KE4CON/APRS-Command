namespace Aprs.Services;

public enum WeatherInputDriverType
{
    Manual,
    FileImport,
    Serial,
    Usb,
    UdpNetwork,
    TcpNetwork,
    HttpRest,
    WebSocket,
    WeatherSoftwareFile,
    Simulation,
    Unknown
}
