namespace Aprs.Services;

public enum DataSourceType
{
    Unknown,
    AprsIs,
    Rf,
    TcpKiss,
    SerialKiss,
    Direwolf,
    Agwpe,
    Replay,
    Simulation,
    Training,
    WeatherDriver,
    Gps,
    ManualEntry,
    FileImport,
    LocalApi,
    Plugin,
    LocalGenerated
}
