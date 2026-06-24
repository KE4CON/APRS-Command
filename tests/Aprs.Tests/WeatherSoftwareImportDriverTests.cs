using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherSoftwareImportDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndReceiveOnly()
    {
        var configuration = WeatherSoftwareImportConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("Weather Software Import", configuration.SourceName);
        Assert.Equal(WeatherSoftwareType.GenericRealtimeTxt, configuration.SoftwareType);
        Assert.Null(configuration.FilePath);
        Assert.Null(configuration.LocalHttpUrl);
        Assert.Contains("Receive-only", configuration.Notes);
    }

    [Fact]
    public void RealtimeTxtPayload_ParsesIntoCommonWeatherObservation()
    {
        var parser = new WeatherSoftwareImportParser();

        var result = parser.Parse(RealtimePayload, EnabledConfiguration(WeatherSoftwareType.CumulusMx), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("Weather Software Import", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.CumulusMx, observation.SourceType);
        Assert.Equal("realtime.txt", observation.StationDeviceId);
        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5.5, observation.WindSpeedMph);
        Assert.Equal(10.2, observation.WindGustMph);
        Assert.Equal(72.4, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(1013.21, observation.BarometricPressureMillibars!.Value, 2);
        Assert.Equal(0.02, observation.RainLastHourInches);
        Assert.Equal(0.15, observation.RainSinceMidnightInches);
        Assert.Equal("realtime.txt", observation.Diagnostics["format"]);
        Assert.Equal(RealtimePayload, observation.RawSourcePayload);
    }

    [Fact]
    public void JsonPayload_ParsesIntoCommonWeatherObservation()
    {
        var result = new WeatherSoftwareImportParser().Parse(JsonPayload, EnabledConfiguration(WeatherSoftwareType.GenericJson), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal(WeatherObservationSourceType.FileImport, observation.SourceType);
        Assert.Equal("WeeWX", observation.StationDeviceId);
        Assert.Equal(190, observation.WindDirectionDegrees);
        Assert.Equal(6.1, observation.WindSpeedMph);
        Assert.Equal(11.4, observation.WindGustMph);
        Assert.Equal(71.8, observation.TemperatureFahrenheit);
        Assert.Equal(55, observation.HumidityPercent);
        Assert.Equal(1012.8, observation.BarometricPressureMillibars);
        Assert.Equal(0.03, observation.RainLastHourInches);
        Assert.Equal(0.18, observation.RainSinceMidnightInches);
        Assert.Equal(300, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(2.1, observation.UvIndex);
    }

    [Fact]
    public void CsvPayload_ParsesIntoCommonWeatherObservation()
    {
        var configuration = EnabledConfiguration(WeatherSoftwareType.GenericCsv) with { Delimiter = "," };

        var result = new WeatherSoftwareImportParser().Parse(CsvPayload, configuration, ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("Weather Display", observation.StationDeviceId);
        Assert.Equal(200, observation.WindDirectionDegrees);
        Assert.Equal(7.2, observation.WindSpeedMph);
        Assert.Equal(12.5, observation.WindGustMph);
        Assert.Equal(73.1, observation.TemperatureFahrenheit);
        Assert.Equal(56, observation.HumidityPercent);
        Assert.Equal(1014.0, observation.BarometricPressureMillibars);
    }

    [Fact]
    public void KeyValuePayload_ParsesIntoCommonWeatherObservation()
    {
        var result = new WeatherSoftwareImportParser().Parse(KeyValuePayload, EnabledConfiguration(WeatherSoftwareType.GenericKeyValueText), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("GenericWX", observation.StationDeviceId);
        Assert.Equal(210, observation.WindDirectionDegrees);
        Assert.Equal(8.3, observation.WindSpeedMph);
        Assert.Equal(13.4, observation.WindGustMph);
        Assert.Equal(74.2, observation.TemperatureFahrenheit);
        Assert.Equal(57, observation.HumidityPercent);
        Assert.Equal(1015.1, observation.BarometricPressureMillibars);
        Assert.Equal(0.04, observation.RainLastHourInches);
        Assert.Equal(0.20, observation.RainLast24HoursInches);
        Assert.Equal(0.21, observation.RainSinceMidnightInches);
        Assert.Equal(315, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(2.5, observation.UvIndex);
        Assert.Equal(1, observation.LightningCount);
        Assert.Equal(4.2, observation.LightningDistanceMiles);
    }

    [Fact]
    public async Task DriverReadsTemporaryFileAndPublishesThroughWeatherFramework()
    {
        using var tempFile = new TemporaryWeatherFile(KeyValuePayload);
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new WeatherSoftwareImportDriver(EnabledConfiguration(WeatherSoftwareType.GenericKeyValueText, tempFile.Path));
        manager.RegisterDriver(driver);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("GenericWX", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("GenericWX");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.WeatherSoftwareFileImport, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal(74, record.TemperatureFahrenheit);
        Assert.Equal(8, record.WindSpeedMph);
        Assert.Equal(13, record.WindGustMph);
        Assert.Equal(4, record.RainLastHourHundredthsInch);
        Assert.Equal(KeyValuePayload, record.RawPayload);
    }

    [Fact]
    public async Task LocalHttpEndpoint_UsesFakeHttpClient()
    {
        var httpClient = new FakeWeatherSoftwareHttpClient(JsonPayload);
        var configuration = EnabledConfiguration(WeatherSoftwareType.LocalHttpEndpoint) with
        {
            FilePath = null,
            LocalHttpUrl = new Uri("http://127.0.0.1/weather.json")
        };
        var driver = new WeatherSoftwareImportDriver(configuration, httpClient);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        Assert.Equal("http://127.0.0.1/weather.json", httpClient.LastUri!.ToString());
        Assert.Equal("WeeWX", driver.LastObservation!.StationDeviceId);
    }

    [Fact]
    public async Task MissingFile_RecordsErrorSafely()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-weather-{Guid.NewGuid():N}.txt");
        var driver = new WeatherSoftwareImportDriver(EnabledConfiguration(WeatherSoftwareType.GenericKeyValueText, missingPath));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("does not exist", driver.LastError!.Message);
    }

    [Fact]
    public void MalformedPayload_DoesNotCrash()
    {
        var result = new WeatherSoftwareImportParser().Parse("not weather data", EnabledConfiguration(WeatherSoftwareType.GenericKeyValueText), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("not recognized", result.Error);
    }

    [Fact]
    public void StaleFileData_IsMarkedStaleByWeatherDriverManager()
    {
        using var tempFile = new TemporaryWeatherFile(KeyValuePayload);
        var oldWrite = ReceivedAt.AddMinutes(-30);
        File.SetLastWriteTimeUtc(tempFile.Path, oldWrite.UtcDateTime);
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new WeatherSoftwareImportDriver(EnabledConfiguration(WeatherSoftwareType.GenericKeyValueText, tempFile.Path));
        manager.RegisterDriver(driver);

        var handled = driver.ProcessPayload(KeyValuePayload, ReceivedAt, oldWrite);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("GenericWX")!.DataState);
    }

    [Fact]
    public void DefaultFieldMappingPlaceholders_AreAvailable()
    {
        var mappings = new WeatherSoftwareImportParser().GetDefaultMappingPlaceholders();

        Assert.Contains(mappings, mapping => mapping.TargetWeatherField == "WindDirectionDegrees");
        Assert.Contains(mappings, mapping => mapping.SourceField == "tempf");
    }

    private static WeatherSoftwareImportConfiguration EnabledConfiguration(
        WeatherSoftwareType softwareType,
        string? filePath = "weather.txt")
    {
        return WeatherSoftwareImportConfiguration.Default with
        {
            Enabled = true,
            SoftwareType = softwareType,
            FilePath = filePath,
            FileStaleThreshold = TimeSpan.FromMinutes(15),
            ReadTimeout = TimeSpan.FromSeconds(5)
        };
    }

    private const string RealtimePayload = "11/06/26 12:00 72.4 50 50 5.5 10.2 180 0.02 0.15 29.92";

    private const string JsonPayload =
        """{"station":"WeeWX","timestamp":1493322445,"winddir":190,"windspeedmph":6.1,"windgustmph":11.4,"tempf":71.8,"humidity":55,"pressuremb":1012.8,"rainlasthourin":0.03,"dailyrainin":0.18,"solarradiation":300,"uv":2.1}""";

    private const string CsvPayload =
        """
        station,winddir,windspeedmph,windgustmph,tempf,humidity,pressuremb,rainlasthourin,dailyrainin
        Weather Display,200,7.2,12.5,73.1,56,1014.0,0.04,0.19
        """;

    private const string KeyValuePayload = "station=GenericWX,winddir=210,windspeedmph=8.3,windgustmph=13.4,tempf=74.2,humidity=57,pressuremb=1015.1,rainlasthourin=0.04,rain24hin=0.20,dailyrainin=0.21,solarradiation=315,uv=2.5,lightningcount=1,lightningdistancemi=4.2";

    private sealed class TemporaryWeatherFile : IDisposable
    {
        public TemporaryWeatherFile(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"weather-import-{Guid.NewGuid():N}.txt");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private sealed class FakeWeatherSoftwareHttpClient : IWeatherSoftwareHttpClient
    {
        private readonly string response;

        public FakeWeatherSoftwareHttpClient(string response)
        {
            this.response = response;
        }

        public Uri? LastUri { get; private set; }

        public Task<string> GetStringAsync(Uri requestUri, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = requestUri;
            return Task.FromResult(response);
        }
    }
}
