using System.Text.Json;
using Aprs.Services;
using AprsCommand.Api;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public sealed class FileHookServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"aprs-file-hooks-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConfigurationDefaultsAreSafe()
    {
        var configuration = FileHookConfiguration.Default;

        Assert.False(configuration.FileHooksEnabled);
        Assert.False(configuration.ImportEnabled);
        Assert.False(configuration.ExportEnabled);
        Assert.True(configuration.RejectInvalidImports);
        Assert.False(configuration.AllowImportedTransmitRequests);
        Assert.False(configuration.HasTransmitCapability);
        Assert.True(configuration.MaximumImportFileSizeBytes > 0);
    }

    [Fact]
    public async Task StartDoesNotRunWhenDisabledByDefault()
    {
        var service = new FileHookService();

        var status = await service.StartAsync();

        Assert.Equal(FileHookState.Stopped, status.State);
        Assert.Contains("disabled", status.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StationExportCreatesExpectedDtoJson()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SeedStations(new StationUpdateDto { Callsign = "N0CALL", DisplayName = "Net Control" });
        var service = new FileHookService(EnabledConfiguration(exportEnabled: true), provider);

        var result = await service.ExportAsync(FileHookExportKind.Stations);

        Assert.True(result.Success);
        Assert.Equal(1, result.ItemCount);
        Assert.True(File.Exists(result.FilePath));
        Assert.Contains("\"schemaVersion\":\"1.0\"", result.Content);
        Assert.Contains("\"callsign\":\"N0CALL\"", result.Content);
    }

    [Fact]
    public async Task WeatherExportCreatesExpectedDtoJson()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SeedWeather(new WeatherObservationDto { StationId = "WX9XYZ", Temperature = 72 });
        var service = new FileHookService(EnabledConfiguration(exportEnabled: true), provider);

        var result = await service.ExportAsync(FileHookExportKind.Weather);

        Assert.True(result.Success);
        Assert.Contains("\"stationId\":\"WX9XYZ\"", result.Content);
        Assert.Contains("\"temperature\":72", result.Content);
    }

    [Fact]
    public async Task ObjectExportCreatesGeoJson()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SubmitObject(new AprsObjectDto
        {
            ObjectName = "CHECKPNT1",
            Latitude = 39.058333,
            Longitude = -84.508333,
            SymbolTable = "/",
            SymbolCode = "-"
        });
        var service = new FileHookService(EnabledConfiguration(exportEnabled: true), provider);

        var result = await service.ExportAsync(FileHookExportKind.Objects);

        Assert.True(result.Success);
        Assert.Contains("\"type\":\"FeatureCollection\"", result.Content);
        Assert.Contains("\"objectName\":\"CHECKPNT1\"", result.Content);
    }

    [Fact]
    public async Task RawPacketExportCreatesLogText()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SubmitRawPacket(new RawPacketDto { RawPacket = "N0CALL>APRS:>Test" });
        var service = new FileHookService(EnabledConfiguration(exportEnabled: true), provider);

        var result = await service.ExportAsync(FileHookExportKind.RawPackets);

        Assert.True(result.Success);
        Assert.Contains("N0CALL>APRS:>Test", result.Content);
    }

    [Fact]
    public async Task ValidStationImportIsAcceptedAndTaggedFileImport()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, allowStations: true), provider);
        var json = JsonSerializer.Serialize(new StationUpdateDto { Callsign = "N0CALL" }, ContractJsonSerializerOptions.Create());

        var result = await service.ImportAsync(FileHookImportKind.Stations, json);

        Assert.True(result.Success);
        var station = Assert.Single(provider.GetStations());
        Assert.Equal("N0CALL", station.Callsign);
        Assert.Equal(ExternalSourceType.FileImport, station.SourceMetadata.SourceType);
        Assert.Equal(ContractDataOrigin.Imported, station.SourceMetadata.Origin);
    }

    [Fact]
    public async Task ValidWeatherImportIsAcceptedAndTaggedFileImport()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, allowWeather: true), provider);
        var json = JsonSerializer.Serialize(new WeatherObservationDto { StationId = "WX9XYZ", Temperature = 70 }, ContractJsonSerializerOptions.Create());

        var result = await service.ImportAsync(FileHookImportKind.Weather, json);

        Assert.True(result.Success);
        var weather = Assert.Single(provider.GetWeather());
        Assert.Equal("WX9XYZ", weather.StationId);
        Assert.Equal(ExternalSourceType.FileImport, weather.SourceMetadata.SourceType);
    }

    [Fact]
    public async Task MissingSourceMetadataIsHandledSafely()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, allowStations: true), provider);

        var result = await service.ImportAsync(FileHookImportKind.Stations, "{\"schemaVersion\":\"1.0\",\"callsign\":\"KD8ABC-7\"}");

        Assert.True(result.Success);
        var station = Assert.Single(provider.GetStations());
        Assert.Equal("File Import", station.SourceMetadata.SourceName);
        Assert.Equal(ExternalTrustLevel.External, station.SourceMetadata.TrustLevel);
    }

    [Fact]
    public async Task InvalidImportIsRejected()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, allowStations: true), provider);

        var result = await service.ImportAsync(FileHookImportKind.Stations, "{\"schemaVersion\":\"1.0\"}");

        Assert.False(result.Success);
        Assert.Equal(1, result.RejectedCount);
        Assert.Empty(provider.GetStations());
        Assert.Contains("callsign", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportedRawPacketDoesNotTriggerTransmit()
    {
        var transmit = new FakeTransmitServices();
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, allowRawPackets: true), provider);

        var result = await service.ImportAsync(FileHookImportKind.RawPackets, "N0CALL>APRS:>Imported");

        Assert.True(result.Success);
        Assert.Single(provider.GetRawPackets());
        Assert.Equal(0, transmit.AprsIsTransmitCalls);
        Assert.Equal(0, transmit.RfTransmitCalls);
    }

    [Fact]
    public async Task TransmitRequestImportIsBlockedByDefault()
    {
        var transmit = new FakeTransmitServices();
        var service = new FileHookService(EnabledConfiguration(importEnabled: true));

        var result = await service.ImportAsync(FileHookImportKind.TransmitRequests, "{\"rawPacket\":\"N0CALL>APRS:>Nope\"}");

        Assert.False(result.Success);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, transmit.AprsIsTransmitCalls);
        Assert.Equal(0, transmit.RfTransmitCalls);
    }

    [Fact]
    public async Task EventBusReceivesImportAndExportEvents()
    {
        var bus = new AprsEventBus();
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SeedStations(new StationUpdateDto { Callsign = "N0CALL" });
        var service = new FileHookService(EnabledConfiguration(importEnabled: true, exportEnabled: true, allowWeather: true), provider, bus);

        await service.ExportAsync(FileHookExportKind.Stations);
        await service.ImportAsync(
            FileHookImportKind.Weather,
            JsonSerializer.Serialize(new WeatherObservationDto { StationId = "WX9XYZ" }, ContractJsonSerializerOptions.Create()));

        var events = bus.GetRecentEvents();
        Assert.Contains(events, evt => evt.Metadata.Summary?.Contains("export completed", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(events, evt => evt.Metadata.EventType == AprsEventType.WeatherUpdated);
    }

    [Fact]
    public async Task ScanImportFolderProcessesAndArchivesFiles()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var configuration = EnabledConfiguration(importEnabled: true, allowStations: true);
        var service = new FileHookService(configuration, provider);
        service.EnsureFolderStructure();
        var incoming = Path.Combine(configuration.ImportFolderPath, "stations", "station.json");
        await File.WriteAllTextAsync(incoming, JsonSerializer.Serialize(new StationUpdateDto { Callsign = "N0CALL" }, ContractJsonSerializerOptions.Create()));

        var result = await service.ScanImportFolderAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.FilesProcessed);
        Assert.Single(provider.GetStations());
        Assert.False(File.Exists(incoming));
        Assert.True(File.Exists(Path.Combine(configuration.BaseFolderPath, "processed", "station.json")));
    }

    private FileHookConfiguration EnabledConfiguration(
        bool importEnabled = false,
        bool exportEnabled = false,
        bool allowStations = false,
        bool allowWeather = false,
        bool allowRawPackets = false)
    {
        return FileHookConfiguration.Default with
        {
            FileHooksEnabled = true,
            ImportEnabled = importEnabled,
            ExportEnabled = exportEnabled,
            BaseFolderPath = root,
            ImportFolderPath = Path.Combine(root, "incoming"),
            ExportFolderPath = Path.Combine(root, "exports"),
            AllowImportedStationData = allowStations,
            AllowImportedWeatherData = allowWeather,
            AllowImportedRawPacketData = allowRawPackets
        };
    }

    private sealed class FakeTransmitServices
    {
        public int AprsIsTransmitCalls { get; private set; }
        public int RfTransmitCalls { get; private set; }
    }
}
