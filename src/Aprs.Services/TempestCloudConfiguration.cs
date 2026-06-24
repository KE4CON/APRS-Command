namespace Aprs.Services;

public sealed record TempestCloudConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    string? AccessTokenReference,
    string? StationId,
    string? DeviceId,
    TimeSpan PollingInterval,
    Uri ApiBaseUrl,
    bool WebSocketEnabled,
    bool RestPollingEnabled,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan RequestTimeout,
    TimeSpan StaleDataThreshold,
    string? Notes)
{
    public static TempestCloudConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "weatherflow-tempest-cloud",
        SourceName: "WeatherFlow Tempest Cloud",
        AccessTokenReference: null,
        StationId: null,
        DeviceId: null,
        PollingInterval: TimeSpan.FromMinutes(5),
        ApiBaseUrl: new Uri("https://swd.weatherflow.com"),
        WebSocketEnabled: false,
        RestPollingEnabled: true,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(30),
        RequestTimeout: TimeSpan.FromSeconds(15),
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        Notes: "Optional cloud polling driver. Requires a user-provided token reference; no token is stored in this configuration.");

    public WeatherInputDriverConfiguration ToDriverConfiguration()
    {
        return new WeatherInputDriverConfiguration(
            DriverId,
            SourceName,
            WeatherInputDriverType.HttpRest,
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
