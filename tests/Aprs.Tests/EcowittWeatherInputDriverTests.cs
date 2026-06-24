using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class EcowittWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndLocalFirst()
    {
        var configuration = EcowittWeatherConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("Ecowitt / Fine Offset / GW1000", configuration.SourceName);
        Assert.Equal(EcowittWeatherDataSourceType.LocalGatewayHttpPolling, configuration.DataSourceType);
        Assert.Equal(80, configuration.GatewayPort);
        Assert.Equal("/get_livedata_info", configuration.ApiPath);
        Assert.Contains("Receive-only", configuration.Notes);
    }

    [Fact]
    public void JsonGatewayPayload_ParsesIntoCommonWeatherObservation()
    {
        var parser = new EcowittWeatherPayloadParser();

        var result = parser.Parse(JsonGatewayPayload, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("Ecowitt / Fine Offset / GW1000", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.EcowittFineOffsetGw1000, observation.SourceType);
        Assert.Equal("GW1000A", observation.StationDeviceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_493_322_445), observation.TimestampUtc);
        Assert.Equal("json", observation.Diagnostics["format"]);
        Assert.Equal("GW1000A", observation.Diagnostics["station_type"]);
        Assert.Equal(JsonGatewayPayload, observation.RawSourcePayload);
    }

    [Fact]
    public void JsonGatewayPayload_MapsWeatherFieldsAndDiagnostics()
    {
        var observation = new EcowittWeatherPayloadParser().Parse(JsonGatewayPayload, EnabledConfiguration(), ReceivedAt).Observation!;

        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5.5, observation.WindSpeedMph);
        Assert.Equal(10.2, observation.WindGustMph);
        Assert.Equal(72.4, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(1013.21, observation.BarometricPressureMillibars!.Value, 2);
        Assert.Equal(0.02, observation.RainLastHourInches);
        Assert.Equal(0.15, observation.RainLast24HoursInches);
        Assert.Equal(0.15, observation.RainSinceMidnightInches);
        Assert.Equal(310, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(2.4, observation.UvIndex);
        Assert.Equal(2, observation.LightningCount);
        Assert.Equal(8.70, observation.LightningDistanceMiles!.Value, 2);
        Assert.Equal("0.7", observation.Diagnostics["weekly_rain_in"]);
        Assert.Equal("1.25", observation.Diagnostics["monthly_rain_in"]);
        Assert.Equal("3.09", observation.Diagnostics["extra_temp1f"]);
        Assert.Equal("51", observation.Diagnostics["extra_humidity1"]);
    }

    [Fact]
    public void FormUploadPayload_IsRecognizedAndMapped()
    {
        var parser = new EcowittWeatherPayloadParser();

        var result = parser.Parse(FormUploadPayload, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("GW1000A", observation.StationDeviceId);
        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5.5, observation.WindSpeedMph);
        Assert.Equal(10.2, observation.WindGustMph);
        Assert.Equal(72.4, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(WeatherObservationSourceType.EcowittFineOffsetGw1000, observation.SourceType);
        Assert.Equal("form", observation.Diagnostics["format"]);
        Assert.Equal(FormUploadPayload, observation.RawSourcePayload);
    }

    [Fact]
    public async Task DriverPollsFakeGatewayAndPublishesThroughWeatherFramework()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var httpClient = new FakeEcowittHttpClient(JsonGatewayPayload);
        var driver = new EcowittWeatherInputDriver(EnabledConfiguration(), httpClient);
        manager.RegisterDriver(driver);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        Assert.Equal("http://192.0.2.10/get_livedata_info", httpClient.LastUri!.ToString());

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("GW1000A", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("GW1000A");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.EcowittFineOffsetGw1000, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(6, record.WindSpeedMph);
        Assert.Equal(10, record.WindGustMph);
        Assert.Equal(2, record.RainLastHourHundredthsInch);
        Assert.Equal(JsonGatewayPayload, record.RawPayload);
    }

    [Fact]
    public void MalformedPayload_DoesNotCrash()
    {
        var result = new EcowittWeatherPayloadParser().Parse("not a payload", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("not recognized", result.Error);
    }

    [Fact]
    public async Task HttpFailure_RecordsErrorSafely()
    {
        var driver = new EcowittWeatherInputDriver(
            EnabledConfiguration(),
            new FakeEcowittHttpClient(new TimeoutException("gateway timeout")));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("timeout", driver.LastError!.Message);
    }

    [Fact]
    public async Task MissingGatewayHost_PreventsPolling()
    {
        var driver = new EcowittWeatherInputDriver(
            EnabledConfiguration() with { GatewayHost = string.Empty },
            new FakeEcowittHttpClient(JsonGatewayPayload));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("host", driver.LastError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidGatewayPort_PreventsPolling()
    {
        var driver = new EcowittWeatherInputDriver(
            EnabledConfiguration() with { GatewayPort = 70000 },
            new FakeEcowittHttpClient(JsonGatewayPayload));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("port", driver.LastError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaleData_IsMarkedStaleByWeatherDriverManager()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new EcowittWeatherInputDriver(EnabledConfiguration(), new FakeEcowittHttpClient(StaleJsonPayload));
        manager.RegisterDriver(driver);

        var handled = driver.ProcessPayload(StaleJsonPayload, ReceivedAt);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("GW1000A")!.DataState);
    }

    [Fact]
    public async Task CustomUploadPlaceholder_FailsSafely()
    {
        var driver = new EcowittWeatherInputDriver(
            EnabledConfiguration() with { DataSourceType = EcowittWeatherDataSourceType.CustomUploadReceiverPlaceholder },
            new FakeEcowittHttpClient(JsonGatewayPayload));

        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("placeholders", driver.LastError!.Message);
    }

    private static EcowittWeatherConfiguration EnabledConfiguration()
    {
        return EcowittWeatherConfiguration.Default with
        {
            Enabled = true,
            GatewayHost = "192.0.2.10",
            StationDeviceId = "GW1000A",
            PollingInterval = TimeSpan.FromMinutes(5)
        };
    }

    private const string JsonGatewayPayload =
        """
        {"stationtype":"GW1000A","dateutc":"1493322445","winddir":180,"windspeedmph":5.5,"windgustmph":10.2,"tempf":72.4,"humidity":50,"baromrelin":29.92,"hourlyrainin":0.02,"eventrainin":0.05,"dailyrainin":0.15,"weeklyrainin":"0.70","monthlyrainin":"1.25","yearlyrainin":"12.5","solarradiation":310,"uv":2.4,"lightning_num":2,"lightning":14.0,"wh65batt":"0","temp1f":"3.09","humidity1":51}
        """;

    private const string StaleJsonPayload =
        """
        {"stationtype":"GW1000A","dateutc":"1493322445","winddir":180,"windspeedmph":5.5,"tempf":72.4,"humidity":50,"baromrelin":29.92}
        """;

    private const string FormUploadPayload =
        "stationtype=GW1000A&dateutc=1493322445&winddir=180&windspeedmph=5.5&windgustmph=10.2&tempf=72.4&humidity=50&baromrelin=29.92&hourlyrainin=0.02&dailyrainin=0.15&solarradiation=310&uv=2.4";

    private sealed class FakeEcowittHttpClient : IEcowittWeatherHttpClient
    {
        private readonly string? response;
        private readonly Exception? exception;

        public FakeEcowittHttpClient(string response)
        {
            this.response = response;
        }

        public FakeEcowittHttpClient(Exception exception)
        {
            this.exception = exception;
        }

        public Uri? LastUri { get; private set; }

        public Task<string> GetStringAsync(Uri requestUri, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = requestUri;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response!);
        }
    }
}
