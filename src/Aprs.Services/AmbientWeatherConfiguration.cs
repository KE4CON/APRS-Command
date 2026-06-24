namespace Aprs.Services;

public sealed record AmbientWeatherConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    AmbientWeatherDataSourceType DataSourceType,
    Uri ApiBaseUrl,
    string? ApplicationKeyReference,
    string? ApiKeyReference,
    string? DeviceId,
    TimeSpan PollingInterval,
    TimeSpan RequestTimeout,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan StaleDataThreshold,
    string? Notes)
{
    public static AmbientWeatherConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "ambient-weather",
        SourceName: "Ambient Weather",
        DataSourceType: AmbientWeatherDataSourceType.AmbientWeatherApi,
        ApiBaseUrl: new Uri("https://api.ambientweather.net"),
        ApplicationKeyReference: null,
        ApiKeyReference: null,
        DeviceId: null,
        PollingInterval: TimeSpan.FromMinutes(5),
        RequestTimeout: TimeSpan.FromSeconds(15),
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(30),
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        Notes: "Optional Ambient Weather API polling driver. Credentials are referenced through secure storage and are not stored here. Local network and file import sources are reserved for future driver variants.");

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
