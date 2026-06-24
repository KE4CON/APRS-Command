using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DavisWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndStoresNoCredentials()
    {
        var configuration = DavisWeatherConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("Davis Weather", configuration.SourceName);
        Assert.Equal(DavisWeatherDataSourceType.WeatherLinkCloudApi, configuration.DataSourceType);
        Assert.Null(configuration.ApiKeyReference);
        Assert.Null(configuration.ApiSecretReference);
        Assert.Contains("not stored", configuration.Notes);
    }

    [Fact]
    public async Task MissingCredentials_PreventPolling()
    {
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration() with { ApiKeyReference = null },
            NullWeatherCredentialStore.Instance,
            new FakeDavisHttpClient(CurrentObservationJson));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("credential", driver.LastError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingStationId_PreventsPolling()
    {
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration() with { StationId = null },
            new FakeCredentialStore("fake-key", "fake-secret"),
            new FakeDavisHttpClient(CurrentObservationJson));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("station ID", driver.LastError!.Message);
    }

    [Fact]
    public void FakeWeatherLinkJson_ParsesIntoCommonWeatherObservation()
    {
        var parser = new DavisWeatherJsonParser();

        var result = parser.Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal("Davis Weather", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.DavisWeatherLink, observation.SourceType);
        Assert.Equal("456", observation.StationDeviceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_493_322_445), observation.TimestampUtc);
        Assert.Equal("123", observation.Diagnostics["station_id"]);
        Assert.Equal("456", observation.Diagnostics["sensor_lsid"]);
        Assert.Equal(CurrentObservationJson, observation.RawSourcePayload);
    }

    [Fact]
    public void FakeWeatherLinkJson_MapsWeatherFieldsAndConvertsPressure()
    {
        var observation = new DavisWeatherJsonParser().Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt).Observation!;

        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5.5, observation.WindSpeedMph);
        Assert.Equal(10.2, observation.WindGustMph);
        Assert.Equal(72.4, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(1013.21, observation.BarometricPressureMillibars!.Value, 2);
        Assert.Equal(0.02, observation.RainLastHourInches);
        Assert.Equal(0.10, observation.RainLast24HoursInches);
        Assert.Equal(0.15, observation.RainSinceMidnightInches);
        Assert.Equal(310, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(2.4, observation.UvIndex);
    }

    [Fact]
    public async Task DriverPollsFakeHttpAndPublishesThroughWeatherFramework()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var httpClient = new FakeDavisHttpClient(CurrentObservationJson);
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-key", "fake-secret"),
            httpClient);
        manager.RegisterDriver(driver);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        Assert.Equal("fake-key", httpClient.LastApiKey);
        Assert.Equal("fake-secret", httpClient.LastApiSecret);
        Assert.Contains("/v2/current/123", httpClient.LastUri!.ToString());

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("456", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("456");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.Davis, record.SourceType);
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
        var result = new DavisWeatherJsonParser().Parse("{not-json", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("valid JSON", result.Error);
    }

    [Fact]
    public async Task HttpFailure_RecordsErrorSafely()
    {
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-key", "fake-secret"),
            new FakeDavisHttpClient(new TimeoutException("weatherlink timeout")));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("timeout", driver.LastError!.Message);
    }

    [Fact]
    public void UnauthorizedResponse_IsReturnedAsFailure()
    {
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-key", "fake-secret"),
            new FakeDavisHttpClient("""{"code":401,"message":"Unauthorized"}"""));

        var handled = driver.ProcessPayload("""{"code":401,"message":"Unauthorized"}""", ReceivedAt);

        Assert.False(handled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("Unauthorized", driver.LastError!.Message);
    }

    [Fact]
    public void StaleData_IsMarkedStaleByWeatherDriverManager()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-key", "fake-secret"),
            new FakeDavisHttpClient(StaleObservationJson));
        manager.RegisterDriver(driver);

        var handled = driver.ProcessPayload(StaleObservationJson, ReceivedAt);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("456")!.DataState);
    }

    [Fact]
    public async Task LocalPlaceholderSources_FailSafely()
    {
        var driver = new DavisWeatherInputDriver(
            EnabledConfiguration() with { DataSourceType = DavisWeatherDataSourceType.LocalHttpIpPlaceholder },
            new FakeCredentialStore("fake-key", "fake-secret"),
            new FakeDavisHttpClient(CurrentObservationJson));

        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("placeholders", driver.LastError!.Message);
    }

    private static DavisWeatherConfiguration EnabledConfiguration()
    {
        return DavisWeatherConfiguration.Default with
        {
            Enabled = true,
            StationId = "123",
            ApiKeyReference = "davis-api-key-ref",
            ApiSecretReference = "davis-api-secret-ref",
            PollingInterval = TimeSpan.FromMinutes(5)
        };
    }

    private const string CurrentObservationJson =
        """
        {"station_id":123,"generated_at":1493322450,"sensors":[{"lsid":456,"sensor_type":45,"data":[{"ts":1493322445,"wind_dir":180,"wind_speed_avg_last_1_min":5.5,"wind_speed_hi_last_10_min":10.2,"temp":72.4,"hum":50,"bar_sea_level":29.92,"rainfall_last_60_min":0.02,"rainfall_last_24_hr":0.10,"rainfall_daily":0.15,"solar_rad":310,"uv_index":2.4,"battery_voltage":4.7}]}]}
        """;

    private const string StaleObservationJson =
        """
        {"station_id":123,"sensors":[{"lsid":456,"sensor_type":45,"data":[{"ts":1493322445,"wind_dir":180,"wind_speed_avg_last_1_min":5.5,"temp":72.4,"hum":50,"bar_sea_level":29.92}]}]}
        """;

    private sealed class FakeCredentialStore : IWeatherCredentialStore
    {
        private readonly string? apiKey;
        private readonly string? apiSecret;

        public FakeCredentialStore(string? apiKey, string? apiSecret)
        {
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;
        }

        public ValueTask<string?> GetSecretAsync(string credentialReference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(credentialReference.Contains("secret", StringComparison.OrdinalIgnoreCase) ? apiSecret : apiKey);
        }
    }

    private sealed class FakeDavisHttpClient : IDavisWeatherLinkHttpClient
    {
        private readonly string? response;
        private readonly Exception? exception;

        public FakeDavisHttpClient(string response)
        {
            this.response = response;
        }

        public FakeDavisHttpClient(Exception exception)
        {
            this.exception = exception;
        }

        public Uri? LastUri { get; private set; }

        public string? LastApiKey { get; private set; }

        public string? LastApiSecret { get; private set; }

        public Task<string> GetStringAsync(
            Uri requestUri,
            string apiKey,
            string apiSecret,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = requestUri;
            LastApiKey = apiKey;
            LastApiSecret = apiSecret;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response!);
        }
    }
}
