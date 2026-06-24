using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AmbientWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndStoresNoCredentials()
    {
        var configuration = AmbientWeatherConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("Ambient Weather", configuration.SourceName);
        Assert.Equal(AmbientWeatherDataSourceType.AmbientWeatherApi, configuration.DataSourceType);
        Assert.Null(configuration.ApplicationKeyReference);
        Assert.Null(configuration.ApiKeyReference);
        Assert.Contains("not stored", configuration.Notes);
    }

    [Fact]
    public async Task MissingCredentials_PreventPolling()
    {
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration() with { ApplicationKeyReference = null },
            NullWeatherCredentialStore.Instance,
            new FakeAmbientHttpClient(CurrentObservationJson));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("credential", driver.LastError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FakeAmbientJson_ParsesIntoCommonWeatherObservation()
    {
        var parser = new AmbientWeatherJsonParser();

        var result = parser.Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("Ambient Weather", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.AmbientWeather, observation.SourceType);
        Assert.Equal("AA:BB:CC:DD:EE:FF", observation.StationDeviceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_493_322_445_000), observation.TimestampUtc);
        Assert.Equal("AA:BB:CC:DD:EE:FF", observation.Diagnostics["mac_address"]);
        Assert.Equal("0.7", observation.Diagnostics["weekly_rain_in"]);
        Assert.Equal(CurrentObservationJson, observation.RawSourcePayload);
    }

    [Fact]
    public void FakeAmbientJson_MapsWeatherFieldsAndConvertsPressure()
    {
        var observation = new AmbientWeatherJsonParser().Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt).Observation!;

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
    }

    [Fact]
    public async Task DriverPollsFakeHttpAndPublishesThroughWeatherFramework()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var httpClient = new FakeAmbientHttpClient(CurrentObservationJson);
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-app-key", "fake-api-key"),
            httpClient);
        manager.RegisterDriver(driver);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        Assert.Equal("fake-app-key", httpClient.LastApplicationKey);
        Assert.Equal("fake-api-key", httpClient.LastApiKey);
        Assert.Contains("/v1/devices/AA%3ABB%3ACC%3ADD%3AEE%3AFF", httpClient.LastUri!.ToString());

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("AA:BB:CC:DD:EE:FF", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("AA:BB:CC:DD:EE:FF");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.AmbientWeather, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(6, record.WindSpeedMph);
        Assert.Equal(10, record.WindGustMph);
        Assert.Equal(2, record.RainLastHourHundredthsInch);
        Assert.Equal(CurrentObservationJson, record.RawPayload);
    }

    [Fact]
    public void MalformedJson_DoesNotCrash()
    {
        var result = new AmbientWeatherJsonParser().Parse("{not-json", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("valid JSON", result.Error);
    }

    [Fact]
    public async Task HttpFailure_RecordsErrorSafely()
    {
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-app-key", "fake-api-key"),
            new FakeAmbientHttpClient(new TimeoutException("ambient timeout")));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("timeout", driver.LastError!.Message);
    }

    [Fact]
    public void UnauthorizedResponse_IsReturnedAsFailure()
    {
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-app-key", "fake-api-key"),
            new FakeAmbientHttpClient("""{"error":"Unauthorized"}"""));

        var handled = driver.ProcessPayload("""{"error":"Unauthorized"}""", ReceivedAt);

        Assert.False(handled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("Unauthorized", driver.LastError!.Message);
    }

    [Fact]
    public void StaleData_IsMarkedStaleByWeatherDriverManager()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-app-key", "fake-api-key"),
            new FakeAmbientHttpClient(StaleObservationJson));
        manager.RegisterDriver(driver);

        var handled = driver.ProcessPayload(StaleObservationJson, ReceivedAt);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("AA:BB:CC:DD:EE:FF")!.DataState);
    }

    [Fact]
    public async Task LocalPlaceholderSources_FailSafely()
    {
        var driver = new AmbientWeatherInputDriver(
            EnabledConfiguration() with { DataSourceType = AmbientWeatherDataSourceType.LocalNetworkPlaceholder },
            new FakeCredentialStore("fake-app-key", "fake-api-key"),
            new FakeAmbientHttpClient(CurrentObservationJson));

        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("placeholders", driver.LastError!.Message);
    }

    private static AmbientWeatherConfiguration EnabledConfiguration()
    {
        return AmbientWeatherConfiguration.Default with
        {
            Enabled = true,
            ApplicationKeyReference = "ambient-application-key-ref",
            ApiKeyReference = "ambient-api-key-ref",
            DeviceId = "AA:BB:CC:DD:EE:FF",
            PollingInterval = TimeSpan.FromMinutes(5)
        };
    }

    private const string CurrentObservationJson =
        """
        [{"macAddress":"AA:BB:CC:DD:EE:FF","dateutc":1493322445000,"winddir":180,"windspeedmph":5.5,"windgustmph":10.2,"tempf":72.4,"humidity":50,"baromrelin":29.92,"hourlyrainin":0.02,"eventrainin":0.05,"dailyrainin":0.15,"weeklyrainin":0.70,"monthlyrainin":1.25,"yearlyrainin":12.5,"solarradiation":310,"uv":2.4,"battout":"1"}]
        """;

    private const string StaleObservationJson =
        """
        [{"macAddress":"AA:BB:CC:DD:EE:FF","dateutc":1493322445000,"winddir":180,"windspeedmph":5.5,"tempf":72.4,"humidity":50,"baromrelin":29.92}]
        """;

    private sealed class FakeCredentialStore : IWeatherCredentialStore
    {
        private readonly string? applicationKey;
        private readonly string? apiKey;

        public FakeCredentialStore(string? applicationKey, string? apiKey)
        {
            this.applicationKey = applicationKey;
            this.apiKey = apiKey;
        }

        public ValueTask<string?> GetSecretAsync(string credentialReference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(credentialReference.Contains("application", StringComparison.OrdinalIgnoreCase) ? applicationKey : apiKey);
        }
    }

    private sealed class FakeAmbientHttpClient : IAmbientWeatherHttpClient
    {
        private readonly string? response;
        private readonly Exception? exception;

        public FakeAmbientHttpClient(string response)
        {
            this.response = response;
        }

        public FakeAmbientHttpClient(Exception exception)
        {
            this.exception = exception;
        }

        public Uri? LastUri { get; private set; }

        public string? LastApplicationKey { get; private set; }

        public string? LastApiKey { get; private set; }

        public Task<string> GetStringAsync(
            Uri requestUri,
            string applicationKey,
            string apiKey,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = requestUri;
            LastApplicationKey = applicationKey;
            LastApiKey = apiKey;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response!);
        }
    }
}
