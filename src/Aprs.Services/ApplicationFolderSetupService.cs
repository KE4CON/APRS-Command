using System.Text.Json;

namespace Aprs.Services;

public sealed class ApplicationFolderSetupService : IApplicationFolderSetupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ApplicationFolderSetupResult PrepareFolders(
        FirstRunSetupConfiguration configuration,
        bool createDefaultConfigurationFiles = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var layout = ApplicationFolderLayout.FromRoot(configuration.ApplicationDataFolderPath);
        var createdFolders = new List<string>();
        var createdFiles = new List<string>();
        var warnings = new List<string>();

        foreach (var folder in layout.AllFolders)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    createdFolders.Add(folder);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                warnings.Add($"Could not create folder '{folder}': {ex.Message}");
            }
        }

        if (createDefaultConfigurationFiles)
        {
            foreach (var file in GetDefaultConfigurationFiles(configuration))
            {
                var path = Path.Combine(layout.ConfigFolderPath, file.RelativePath);
                try
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, file.Content);
                        createdFiles.Add(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    warnings.Add($"Could not create configuration file '{path}': {ex.Message}");
                }
            }
        }

        return new ApplicationFolderSetupResult(layout, configuration, createdFolders, createdFiles, warnings);
    }

    public IReadOnlyList<FirstRunDefaultConfigurationFile> GetDefaultConfigurationFiles(
        FirstRunSetupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return
        [
            CreateFile("appsettings.safe-defaults.json", "Application safe defaults", new
            {
                applicationName = "APRS Command",
                firstRunCompleted = configuration.FirstRunCompleted,
                transmitEnabled = false,
                aprsIsTransmitEnabled = false,
                rfTransmitEnabled = false,
                beaconingEnabled = false,
                weatherBeaconingEnabled = false
            }),
            CreateFile("station-profile.placeholder.json", "Local station profile placeholder", new
            {
                callsign = string.Empty,
                ssid = (int?)null,
                comment = "APRS Command",
                configured = false,
                note = "Enter your licensed amateur-radio callsign before enabling any transmit feature."
            }),
            CreateFile("aprs-is.placeholder.json", "APRS-IS settings placeholder", new
            {
                server = "rotate.aprs2.net",
                port = 14580,
                callsign = string.Empty,
                passcodeReference = (string?)null,
                receiveOnly = true,
                transmitEnabled = false
            }),
            CreateFile("rf-tnc.placeholder.json", "RF/TNC settings placeholder", new
            {
                tcpKissEnabled = false,
                serialKissEnabled = false,
                agwpeEnabled = false,
                rfTransmitEnabled = false,
                selectedPort = (string?)null
            }),
            CreateFile("map-cache.placeholder.json", "Map cache settings placeholder", new
            {
                cacheEnabled = true,
                cacheFolder = configuration.MapCacheFolderPath,
                allowInternetTileDownload = false
            }),
            CreateFile("safety.safe-defaults.json", "Central safety defaults", new
            {
                transmitEnabled = false,
                aprsIsTransmitEnabled = false,
                rfTransmitEnabled = false,
                iGateEnabled = false,
                digipeaterEnabled = false,
                beaconingEnabled = false,
                weatherBeaconingEnabled = false,
                requireExplicitConfirmation = true
            }),
            CreateFile("extensions.safe-defaults.json", "Extension and local API safe defaults", new
            {
                restApiEnabled = false,
                webSocketEnabled = false,
                fileHooksEnabled = false,
                pluginLoadingEnabled = false,
                allowTransmitRequests = false
            })
        ];
    }

    private static FirstRunDefaultConfigurationFile CreateFile(string relativePath, string description, object model)
    {
        return new FirstRunDefaultConfigurationFile(relativePath, description, JsonSerializer.Serialize(model, JsonOptions));
    }
}
