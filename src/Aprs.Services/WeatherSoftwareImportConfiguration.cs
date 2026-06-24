namespace Aprs.Services;

public sealed record WeatherSoftwareImportConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    WeatherSoftwareType SoftwareType,
    string? FilePath,
    string? DirectoryPath,
    Uri? LocalHttpUrl,
    TimeSpan PollingInterval,
    TimeSpan FileStaleThreshold,
    bool RetryEnabled,
    TimeSpan ReadTimeout,
    string EncodingName,
    string Delimiter,
    string? Notes)
{
    public static WeatherSoftwareImportConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "weather-software-import",
        SourceName: "Weather Software Import",
        SoftwareType: WeatherSoftwareType.GenericRealtimeTxt,
        FilePath: null,
        DirectoryPath: null,
        LocalHttpUrl: null,
        PollingInterval: TimeSpan.FromMinutes(5),
        FileStaleThreshold: TimeSpan.FromMinutes(15),
        RetryEnabled: true,
        ReadTimeout: TimeSpan.FromSeconds(10),
        EncodingName: "utf-8",
        Delimiter: ",",
        Notes: "Receive-only local weather software import driver. Field mapping is intentionally minimal and can be extended later with user-configurable mapping rules.");

    public WeatherInputDriverConfiguration ToDriverConfiguration()
    {
        var driverType = SoftwareType == WeatherSoftwareType.LocalHttpEndpoint
            ? WeatherInputDriverType.HttpRest
            : WeatherInputDriverType.WeatherSoftwareFile;

        return new WeatherInputDriverConfiguration(
            DriverId,
            SourceName,
            driverType,
            Enabled,
            SourceName,
            FileStaleThreshold,
            RetryEnabled,
            ReconnectDelay: TimeSpan.FromSeconds(30),
            ConnectionTimeout: ReadTimeout,
            ReadTimeout,
            Notes);
    }
}
