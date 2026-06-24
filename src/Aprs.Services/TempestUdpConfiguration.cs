namespace Aprs.Services;

public sealed record TempestUdpConfiguration(
    bool Enabled,
    int ListenPort,
    string BindAddress,
    string SourceName,
    string DriverId,
    TimeSpan StaleDataThreshold,
    bool RestartEnabled,
    TimeSpan RestartDelay,
    TimeSpan ReceiveTimeout,
    string? Notes)
{
    public static TempestUdpConfiguration Default { get; } = new(
        Enabled: false,
        ListenPort: 50222,
        BindAddress: "0.0.0.0",
        SourceName: "WeatherFlow Tempest UDP",
        DriverId: "weatherflow-tempest-udp",
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        RestartEnabled: true,
        RestartDelay: TimeSpan.FromSeconds(30),
        ReceiveTimeout: TimeSpan.FromSeconds(10),
        Notes: "Local WeatherFlow Tempest UDP broadcast receiver. Receive only; no transmit support.");

    public WeatherInputDriverConfiguration ToDriverConfiguration()
    {
        return new WeatherInputDriverConfiguration(
            DriverId,
            SourceName,
            WeatherInputDriverType.UdpNetwork,
            Enabled,
            SourceName,
            StaleDataThreshold,
            RestartEnabled,
            RestartDelay,
            ConnectionTimeout: TimeSpan.Zero,
            ReceiveTimeout,
            Notes);
    }
}
