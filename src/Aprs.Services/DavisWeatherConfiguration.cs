namespace Aprs.Services;

public sealed record DavisWeatherConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    DavisWeatherDataSourceType DataSourceType,
    Uri ApiBaseUrl,
    string? StationId,
    string? ApiKeyReference,
    string? ApiSecretReference,
    TimeSpan PollingInterval,
    TimeSpan RequestTimeout,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan StaleDataThreshold,
    string? Notes)
{
    public static DavisWeatherConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "davis-weatherlink",
        SourceName: "Davis Weather",
        DataSourceType: DavisWeatherDataSourceType.WeatherLinkCloudApi,
        ApiBaseUrl: new Uri("https://api.weatherlink.com"),
        StationId: null,
        ApiKeyReference: null,
        ApiSecretReference: null,
        PollingInterval: TimeSpan.FromMinutes(5),
        RequestTimeout: TimeSpan.FromSeconds(15),
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(30),
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        Notes: "Optional Davis WeatherLink cloud polling driver. Credentials are referenced through secure storage and are not stored here. Local logger/IP/file support is reserved for future driver variants.");

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
