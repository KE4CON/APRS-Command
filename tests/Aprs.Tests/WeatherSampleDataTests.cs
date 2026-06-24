using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherSampleDataTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WeatherSampleDataFolderAndReadmeExist()
    {
        Assert.True(Directory.Exists(WeatherDataRoot));
        Assert.True(File.Exists(Path.Combine(WeatherDataRoot, "README.md")));

        var readme = Read("README.md");
        Assert.Contains("fake deterministic test fixtures", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No real API keys", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TempestUdpSamples_ParseOfflineAndPreserveDiagnostics()
    {
        var parser = new TempestUdpJsonParser();
        var configuration = TempestUdpConfiguration.Default with { Enabled = true };
        var obsPayload = Read("Tempest", "udp_obs_st.json");

        var obsResult = parser.Parse(obsPayload, configuration, ReceivedAt);
        var rapidWindResult = parser.Parse(Read("Tempest", "udp_rapid_wind.json"), configuration, ReceivedAt);
        var precipResult = parser.Parse(Read("Tempest", "udp_evt_precip.json"), configuration, ReceivedAt);
        var strikeResult = parser.Parse(Read("Tempest", "udp_evt_strike.json"), configuration, ReceivedAt);
        var deviceStatusResult = parser.Parse(Read("Tempest", "udp_device_status.json"), configuration, ReceivedAt);
        var hubStatusResult = parser.Parse(Read("Tempest", "udp_hub_status.json"), configuration, ReceivedAt);

        Assert.True(obsResult.IsHandled);
        Assert.Equal(WeatherObservationSourceType.WeatherFlowTempest, obsResult.Observation!.SourceType);
        Assert.Equal("ST-TEST0001", obsResult.Observation.StationDeviceId);
        Assert.Equal("HB-TEST0001", obsResult.Observation.Diagnostics["hub_sn"]);
        Assert.Equal(obsPayload, obsResult.Observation.RawSourcePayload);
        Assert.Equal(7.74, obsResult.Observation.WindSpeedMph!.Value, 2);
        Assert.Equal(72.27, obsResult.Observation.TemperatureFahrenheit!.Value, 2);
        Assert.Equal(0.47, obsResult.Observation.RainLastHourInches!.Value, 2);
        Assert.True(rapidWindResult.IsHandled);
        Assert.Equal(128, rapidWindResult.Observation!.WindDirectionDegrees);
        Assert.Equal("evt_precip", precipResult.Diagnostics["message_type"]);
        Assert.Equal("evt_strike", strikeResult.Diagnostics["message_type"]);
        Assert.Equal("device_status", deviceStatusResult.Diagnostics["message_type"]);
        Assert.Equal("hub_status", hubStatusResult.Diagnostics["message_type"]);
    }

    [Fact]
    public void TempestCloudSample_NormalizesMetricFields()
    {
        var result = new TempestCloudJsonParser().Parse(
            Read("Tempest", "cloud_current_observation.json"),
            TempestCloudConfiguration.Default with
            {
                Enabled = true,
                AccessTokenReference = "fake-token-ref",
                DeviceId = "12345",
                StationId = "67890"
            },
            ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal(WeatherObservationSourceType.WeatherFlowTempest, observation.SourceType);
        Assert.Equal("12345", observation.StationDeviceId);
        Assert.Equal(7.74, observation.WindSpeedMph!.Value, 2);
        Assert.Equal(9.51, observation.WindGustMph!.Value, 2);
        Assert.Equal(72.27, observation.TemperatureFahrenheit!.Value, 2);
        Assert.Equal(0.05, observation.RainLastHourInches!.Value, 2);
        Assert.Equal(0.47, observation.RainSinceMidnightInches!.Value, 2);
        Assert.Equal(8.70, observation.LightningDistanceMiles!.Value, 2);
    }

    [Fact]
    public void PeetBrosSamples_ParseKeyValueAndAprsStyleWeatherText()
    {
        var parser = new PeetBrosWeatherParser();
        var configuration = PeetBrosConfiguration.Default with { Enabled = true, SerialPortName = "TEST" };

        var keyValueResult = parser.Parse(Read("PeetBros", "ultimeter_key_value.txt"), configuration, ReceivedAt);
        var aprsResult = parser.Parse(Read("PeetBros", "ultimeter_aprs_weather.txt"), configuration, ReceivedAt);

        Assert.True(keyValueResult.IsHandled);
        Assert.Equal(WeatherObservationSourceType.PeetBrosUltimeter, keyValueResult.Observation!.SourceType);
        Assert.Equal("ULTIMETER2100", keyValueResult.Observation.StationDeviceId);
        Assert.Equal("key-value", keyValueResult.Observation.Diagnostics["format"]);
        Assert.Equal(1013.2, keyValueResult.Observation.BarometricPressureMillibars);

        Assert.True(aprsResult.IsHandled);
        Assert.Equal("aprs-weather", aprsResult.Observation!.Diagnostics["format"]);
        Assert.Equal(72, aprsResult.Observation.TemperatureFahrenheit);
        Assert.Equal(0.10, aprsResult.Observation.RainLast24HoursInches);
    }

    [Fact]
    public void DavisAmbientAndEcowittSamples_NormalizePressureAndSourceTypes()
    {
        var davis = new DavisWeatherJsonParser().Parse(
            Read("Davis", "weatherlink_current.json"),
            DavisWeatherConfiguration.Default with { Enabled = true, StationId = "123" },
            ReceivedAt).Observation!;
        var ambient = new AmbientWeatherJsonParser().Parse(
            Read("Ambient", "current.json"),
            AmbientWeatherConfiguration.Default with { Enabled = true, DeviceId = "AA:BB:CC:DD:EE:FF" },
            ReceivedAt).Observation!;
        var ecowitt = new EcowittWeatherPayloadParser().Parse(
            Read("Ecowitt", "gw1000_livedata.json"),
            EcowittWeatherConfiguration.Default with { Enabled = true, GatewayHost = "192.0.2.10", StationDeviceId = "GW1000A" },
            ReceivedAt).Observation!;
        var ecowittForm = new EcowittWeatherPayloadParser().Parse(
            Read("Ecowitt", "gw1000_upload_form.txt"),
            EcowittWeatherConfiguration.Default with { Enabled = true, GatewayHost = "192.0.2.10", StationDeviceId = "GW1000A" },
            ReceivedAt).Observation!;

        Assert.Equal(WeatherObservationSourceType.DavisWeatherLink, davis.SourceType);
        Assert.Equal(1013.21, davis.BarometricPressureMillibars!.Value, 2);
        Assert.Equal(WeatherObservationSourceType.AmbientWeather, ambient.SourceType);
        Assert.Equal(1013.21, ambient.BarometricPressureMillibars!.Value, 2);
        Assert.Equal(WeatherObservationSourceType.EcowittFineOffsetGw1000, ecowitt.SourceType);
        Assert.Equal("json", ecowitt.Diagnostics["format"]);
        Assert.Equal("form", ecowittForm.Diagnostics["format"]);
        Assert.Equal(2, ecowitt.LightningCount);
    }

    [Theory]
    [InlineData(WeatherSoftwareType.CumulusMx, "WeatherSoftware", "cumulus_realtime.txt", WeatherObservationSourceType.CumulusMx, "realtime.txt")]
    [InlineData(WeatherSoftwareType.GenericJson, "WeatherSoftware", "weewx.json", WeatherObservationSourceType.FileImport, "json")]
    [InlineData(WeatherSoftwareType.GenericCsv, "WeatherSoftware", "weather_display.csv", WeatherObservationSourceType.FileImport, "csv")]
    [InlineData(WeatherSoftwareType.GenericKeyValueText, "WeatherSoftware", "generic_key_value.txt", WeatherObservationSourceType.FileImport, "key-value")]
    [InlineData(WeatherSoftwareType.LocalHttpEndpoint, "WeatherSoftware", "generic_http.json", WeatherObservationSourceType.FileImport, "json")]
    public void WeatherSoftwareSamples_ParseOffline(
        WeatherSoftwareType softwareType,
        string folder,
        string fileName,
        WeatherObservationSourceType expectedSourceType,
        string expectedFormat)
    {
        var configuration = WeatherSoftwareImportConfiguration.Default with
        {
            Enabled = true,
            SoftwareType = softwareType
        };

        var result = new WeatherSoftwareImportParser().Parse(Read(folder, fileName), configuration, ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(expectedSourceType, result.Observation!.SourceType);
        Assert.Equal(expectedFormat, result.Observation.Diagnostics["format"]);
        Assert.NotNull(result.Observation.RawSourcePayload);
        Assert.NotEqual(default, result.Observation.TimestampUtc);
    }

    [Fact]
    public void InvalidSamples_DoNotCrashAndReturnFailures()
    {
        var tempestResult = new TempestUdpJsonParser().Parse(
            Read("Invalid", "malformed_json.txt"),
            TempestUdpConfiguration.Default with { Enabled = true },
            ReceivedAt);
        var peetResult = new PeetBrosWeatherParser().Parse(
            Read("Invalid", "malformed_key_value.txt"),
            PeetBrosConfiguration.Default with { Enabled = true },
            ReceivedAt);
        var softwareResult = new WeatherSoftwareImportParser().Parse(
            Read("Invalid", "missing_required_weather_fields.json"),
            WeatherSoftwareImportConfiguration.Default with { Enabled = true, SoftwareType = WeatherSoftwareType.GenericJson },
            ReceivedAt);

        Assert.False(tempestResult.IsHandled);
        Assert.Null(tempestResult.Observation);
        Assert.False(peetResult.IsHandled);
        Assert.Null(peetResult.Observation);
        Assert.True(softwareResult.IsHandled);
        Assert.Null(softwareResult.Observation!.WindDirectionDegrees);
        var formatResult = new AprsWeatherFormatter().FormatPreview(softwareResult.Observation with { Callsign = "N0CALL" });
        Assert.False(formatResult.IsSuccess);
        Assert.Contains(formatResult.ValidationErrors, error => error.Contains("Wind direction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DriverManagerMarksSampleStalePayloadsAsStaleAndPreservesDriverId()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new TempestUdpWeatherInputDriver(TempestUdpConfiguration.Default with { Enabled = true });
        manager.RegisterDriver(driver);

        driver.ProcessPayload(Read("Tempest", "udp_obs_st.json"), ReceivedAt);

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(driver.DriverId, snapshot.DriverId);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("ST-TEST0001")!.DataState);
    }

    [Fact]
    public void AprsWeatherSamples_ParseAndFormatterRejectsUnsafeInputs()
    {
        var parser = new AprsParser();
        var packet = parser.Parse(Read("AprsWeather", "position_weather_packet.txt").Trim(), ReceivedAt);
        Assert.IsType<WeatherAprsPacket>(packet);

        var formatter = new AprsWeatherFormatter();
        var valid = formatter.FormatPreview(CreateFormatterObservation());
        var stale = formatter.FormatPreview(CreateFormatterObservation(staleDataState: WeatherDataState.Stale));
        var badHumidity = formatter.FormatPreview(CreateFormatterObservation(humidity: 101));
        var badWind = formatter.FormatPreview(CreateFormatterObservation(windDirection: 999));
        var badRain = formatter.FormatPreview(CreateFormatterObservation(rainLastHourInches: -0.01));
        var missingCallsign = formatter.FormatPreview(CreateFormatterObservation(callsign: null));
        var missingFields = formatter.FormatPreview(CreateFormatterObservation(windDirection: null, humidity: null));
        var badComment = formatter.FormatPreview(
            CreateFormatterObservation(),
            options: AprsWeatherFormatterOptions.Default with { Comment = "bad\ncomment" });

        Assert.True(valid.IsSuccess);
        Assert.NotNull(valid.Packet);
        Assert.DoesNotContain('\n', valid.Packet);
        Assert.DoesNotContain('\r', valid.Packet);
        Assert.False(stale.IsSuccess);
        Assert.Contains(stale.ValidationErrors, error => error.Contains("Stale", StringComparison.OrdinalIgnoreCase));
        Assert.False(badHumidity.IsSuccess);
        Assert.False(badWind.IsSuccess);
        Assert.False(badRain.IsSuccess);
        Assert.False(missingCallsign.IsSuccess);
        Assert.False(missingFields.IsSuccess);
        Assert.False(badComment.IsSuccess);
    }

    private static CommonWeatherObservation CreateFormatterObservation(
        string? callsign = "N0CALL",
        int? windDirection = 180,
        int? humidity = 50,
        double? rainLastHourInches = 0,
        WeatherDataState staleDataState = WeatherDataState.Current)
    {
        return new CommonWeatherObservation(
            "Sample Weather",
            WeatherObservationSourceType.Manual,
            "sample-weather",
            callsign,
            ReceivedAt,
            39.058333,
            -84.508333,
            windDirection,
            5,
            10,
            72,
            rainLastHourInches,
            0,
            0,
            humidity,
            1013.2,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string>(),
            RawSourcePayload: "sample",
            staleDataState,
            ValidationErrors: [],
            ValidationWarnings: []);
    }

    private static string Read(params string[] pathSegments)
    {
        return File.ReadAllText(Path.Combine([WeatherDataRoot, .. pathSegments]));
    }

    private static string WeatherDataRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "tests", "Aprs.Tests", "TestData", "Weather");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate tests/Aprs.Tests/TestData/Weather.");
        }
    }
}
