namespace Aprs.Services;

public sealed record EcowittWeatherConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    EcowittWeatherDataSourceType DataSourceType,
    string GatewayHost,
    int GatewayPort,
    TimeSpan PollingInterval,
    TimeSpan RequestTimeout,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan StaleDataThreshold,
    string? StationDeviceId,
    string ApiPath,
    string? Notes)
{
    public static EcowittWeatherConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "ecowitt-fine-offset-gw1000",
        SourceName: "Ecowitt / Fine Offset / GW1000",
        DataSourceType: EcowittWeatherDataSourceType.LocalGatewayHttpPolling,
        GatewayHost: string.Empty,
        GatewayPort: 80,
        PollingInterval: TimeSpan.FromMinutes(5),
        RequestTimeout: TimeSpan.FromSeconds(10),
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(30),
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        StationDeviceId: null,
        ApiPath: "/get_livedata_info",
        Notes: "Receive-only local Ecowitt/Fine Offset/GW1000 gateway polling driver. Custom upload receiver and file import sources are reserved for future local API/import work.");

    public WeatherInputDriverConfiguration ToDriverConfiguration()
    {
        return new WeatherInputDriverConfiguration(
            DriverId,
            SourceName,
            DataSourceType == EcowittWeatherDataSourceType.LocalGatewayHttpPolling ? WeatherInputDriverType.HttpRest : WeatherInputDriverType.Unknown,
            Enabled,
            SourceName,
            StaleDataThreshold,
            ReconnectEnabled,
            ReconnectDelay,
            RequestTimeout,
            RequestTimeout,
            Notes);
    }
}
