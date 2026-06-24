namespace Aprs.Services;

public record WeatherInputDriverConfiguration(
    string DriverId,
    string DriverName,
    WeatherInputDriverType DriverType,
    bool Enabled,
    string SourceName,
    TimeSpan StaleDataThreshold,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan ConnectionTimeout,
    TimeSpan ReadTimeout,
    string? Notes)
{
    public static WeatherInputDriverConfiguration CreateDefault(
        string driverId,
        string driverName,
        WeatherInputDriverType driverType = WeatherInputDriverType.Manual)
    {
        return new WeatherInputDriverConfiguration(
            driverId,
            driverName,
            driverType,
            Enabled: false,
            SourceName: driverName,
            StaleDataThreshold: TimeSpan.FromMinutes(15),
            ReconnectEnabled: true,
            ReconnectDelay: TimeSpan.FromSeconds(30),
            ConnectionTimeout: TimeSpan.FromSeconds(10),
            ReadTimeout: TimeSpan.FromSeconds(10),
            Notes: null);
    }
}
