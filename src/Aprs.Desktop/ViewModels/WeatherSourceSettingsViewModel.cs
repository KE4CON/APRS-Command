using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherSourceSettingsViewModel
{
    public WeatherSourceSettingsViewModel(WeatherSourceSetupOptionViewModel source)
    {
        SourceName = source.DisplayName;
        SourceKey = source.Key;
        SettingsTitle = BuildTitle(source.Key);
        SettingsSummary = BuildSummary(source.Key);
        Rows = BuildRows(source.Key);
        CredentialWarning = RequiresCredential(source.Key)
            ? "Credential/token values are references only and should not be committed or shown plainly."
            : "No credentials are required for this source panel.";
    }

    public string SourceName { get; }

    public string SourceKey { get; }

    public string SettingsTitle { get; }

    public string SettingsSummary { get; }

    public IReadOnlyList<string> Rows { get; }

    public string CredentialWarning { get; }

    public bool ShowsTempestUdpSettings => SourceKey == "tempest-udp";

    public bool ShowsTempestCloudSettings => SourceKey == "tempest-cloud";

    public bool ShowsPeetBrosSettings => SourceKey == "peet-bros";

    public bool ShowsDavisSettings => SourceKey == "davis";

    public bool ShowsAmbientSettings => SourceKey == "ambient";

    public bool ShowsEcowittSettings => SourceKey == "ecowitt";

    public bool ShowsWeatherSoftwareSettings => SourceKey.StartsWith("software-", StringComparison.Ordinal);

    public bool ShowsManualSettings => SourceKey == "manual";

    public bool ShowsSimulationSettings => SourceKey == "simulation";

    private static string BuildTitle(string key)
    {
        return key switch
        {
            "tempest-udp" => "WeatherFlow Tempest Local UDP",
            "tempest-cloud" => "WeatherFlow Tempest Cloud API",
            "peet-bros" => "Peet Bros ULTIMETER Serial",
            "davis" => "Davis WeatherLink",
            "ambient" => "Ambient Weather",
            "ecowitt" => "Ecowitt / Fine Offset / GW1000",
            "manual" => "Manual Weather Entry",
            "simulation" => "Simulation/Test Weather Source",
            _ when key.StartsWith("software-", StringComparison.Ordinal) => "Weather Software / File Import",
            _ => "Weather Source Settings"
        };
    }

    private static string BuildSummary(string key)
    {
        return key switch
        {
            "tempest-udp" => "Local LAN UDP broadcast receiver. Default listen port 50222; no internet token required.",
            "tempest-cloud" => "Optional cloud polling source. Store only token references, never raw tokens.",
            "peet-bros" => "Serial/text weather input using a reusable serial abstraction. No transmit support here.",
            "davis" => "WeatherLink/cloud-oriented setup placeholder with station ID and credential references.",
            "ambient" => "Ambient Weather API setup placeholder with application/API key references.",
            "ecowitt" => "Local gateway HTTP polling setup with host, port, path, and custom-upload placeholder.",
            "manual" => "Manual observation entry for testing and fallback weather reports.",
            "simulation" => "Safe test source for UI and parser validation without hardware.",
            _ when key.StartsWith("software-", StringComparison.Ordinal) => "File, realtime.txt, JSON, CSV, key-value, or local HTTP import settings.",
            _ => "Select a source to view setup fields."
        };
    }

    private static IReadOnlyList<string> BuildRows(string key)
    {
        return key switch
        {
            "tempest-udp" =>
            [
                $"Enabled: {TempestUdpConfiguration.Default.Enabled}",
                $"UDP listen port: {TempestUdpConfiguration.Default.ListenPort}",
                $"Bind address: {TempestUdpConfiguration.Default.BindAddress}",
                $"Source name: {TempestUdpConfiguration.Default.SourceName}",
                $"Stale threshold: {FormatTimeSpan(TempestUdpConfiguration.Default.StaleDataThreshold)}",
                "Status / last packet: shown in diagnostics"
            ],
            "tempest-cloud" =>
            [
                $"Enabled: {TempestCloudConfiguration.Default.Enabled}",
                "Station/device ID: not set",
                "Access token reference: not set",
                $"Polling interval: {FormatTimeSpan(TempestCloudConfiguration.Default.PollingInterval)}",
                $"Request timeout: {FormatTimeSpan(TempestCloudConfiguration.Default.RequestTimeout)}",
                "Connection status: disconnected"
            ],
            "peet-bros" =>
            [
                $"Enabled: {PeetBrosConfiguration.Default.Enabled}",
                "Serial port: not set",
                $"Baud rate: {PeetBrosConfiguration.Default.BaudRate}",
                "Model name: not set",
                $"Stale threshold: {FormatTimeSpan(PeetBrosConfiguration.Default.StaleDataThreshold)}",
                "Status / last payload: shown in diagnostics"
            ],
            "davis" =>
            [
                $"Enabled: {DavisWeatherConfiguration.Default.Enabled}",
                $"Weather source: {DavisWeatherConfiguration.Default.DataSourceType}",
                "Station ID: not set",
                "API key/token reference: not set",
                $"Polling interval: {FormatTimeSpan(DavisWeatherConfiguration.Default.PollingInterval)}",
                "Status: disconnected"
            ],
            "ambient" =>
            [
                $"Enabled: {AmbientWeatherConfiguration.Default.Enabled}",
                "Application key reference: not set",
                "API key reference: not set",
                "Device ID/MAC: not set",
                $"Polling interval: {FormatTimeSpan(AmbientWeatherConfiguration.Default.PollingInterval)}",
                "Status: disconnected"
            ],
            "ecowitt" =>
            [
                $"Enabled: {EcowittWeatherConfiguration.Default.Enabled}",
                "Gateway host/IP: not set",
                $"Gateway port: {EcowittWeatherConfiguration.Default.GatewayPort}",
                $"Local gateway path: {EcowittWeatherConfiguration.Default.ApiPath}",
                $"Polling interval: {FormatTimeSpan(EcowittWeatherConfiguration.Default.PollingInterval)}",
                "Custom upload receiver: placeholder"
            ],
            "manual" =>
            [
                "Temperature: required",
                "Humidity: required",
                "Wind direction/speed/gust: required for APRS weather",
                "Rain and pressure: required where available",
                "Timestamp and source name: required",
                "Validation status: shown below"
            ],
            "simulation" =>
            [
                "Enabled: false",
                "Source name: Simulation/Test Weather",
                "No hardware, internet, credentials, serial port, or transmit path required"
            ],
            _ when key.StartsWith("software-", StringComparison.Ordinal) =>
            [
                $"Enabled: {WeatherSoftwareImportConfiguration.Default.Enabled}",
                $"Software type: {ResolveSoftwareType(key)}",
                "File path / local HTTP URL: not set",
                $"Polling interval: {FormatTimeSpan(WeatherSoftwareImportConfiguration.Default.PollingInterval)}",
                $"Stale-file threshold: {FormatTimeSpan(WeatherSoftwareImportConfiguration.Default.FileStaleThreshold)}",
                $"Encoding / delimiter: {WeatherSoftwareImportConfiguration.Default.EncodingName} / {WeatherSoftwareImportConfiguration.Default.Delimiter}"
            ],
            _ => ["Select a weather source."]
        };
    }

    private static bool RequiresCredential(string key)
    {
        return key is "tempest-cloud" or "davis" or "ambient";
    }

    private static string ResolveSoftwareType(string key)
    {
        return key switch
        {
            "software-cumulus" => WeatherSoftwareType.CumulusMx.ToString(),
            "software-weewx" => WeatherSoftwareType.WeeWx.ToString(),
            "software-weather-display" => WeatherSoftwareType.WeatherDisplay.ToString(),
            "software-realtime" => WeatherSoftwareType.GenericRealtimeTxt.ToString(),
            "software-json" => WeatherSoftwareType.GenericJson.ToString(),
            "software-csv" => WeatherSoftwareType.GenericCsv.ToString(),
            "software-key-value" => WeatherSoftwareType.GenericKeyValueText.ToString(),
            "software-http" => WeatherSoftwareType.LocalHttpEndpoint.ToString(),
            _ => WeatherSoftwareType.Unknown.ToString()
        };
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        return value.TotalMinutes >= 1 ? $"{value.TotalMinutes:0} min" : $"{value.TotalSeconds:0} sec";
    }
}
