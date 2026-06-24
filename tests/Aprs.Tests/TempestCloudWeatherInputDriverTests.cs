using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class TempestCloudWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndDoesNotStoreToken()
    {
        var configuration = TempestCloudConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("WeatherFlow Tempest Cloud", configuration.SourceName);
        Assert.True(configuration.RestPollingEnabled);
        Assert.False(configuration.WebSocketEnabled);
        Assert.Null(configuration.AccessTokenReference);
        Assert.Contains("no token is stored", configuration.Notes);
    }

    [Fact]
    public async Task MissingToken_PreventsPolling()
    {
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration() with { AccessTokenReference = null },
            NullWeatherCredentialStore.Instance,
            new FakeTempestHttpClient(CurrentObservationJson));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("token", driver.LastError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FakeHttpResponse_ParsesIntoCommonWeatherObservation()
    {
        var parser = new TempestCloudJsonParser();

        var result = parser.Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.NotNull(result.Observation);
        var observation = result.Observation;
        Assert.Equal("WeatherFlow Tempest Cloud", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.WeatherFlowTempest, observation.SourceType);
        Assert.Equal("12345", observation.StationDeviceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_493_322_445), observation.TimestampUtc);
        Assert.Equal(CurrentObservationJson, observation.RawSourcePayload);
    }

    [Fact]
    public void CurrentObservation_MapsWeatherFieldsAndConvertsUnits()
    {
        var observation = new TempestCloudJsonParser().Parse(CurrentObservationJson, EnabledConfiguration(), ReceivedAt).Observation!;

        Assert.Equal(187, observation.WindDirectionDegrees);
        Assert.Equal(7.74, observation.WindSpeedMph!.Value, 2);
        Assert.Equal(9.51, observation.WindGustMph!.Value, 2);
        Assert.Equal(72.27, observation.TemperatureFahrenheit!.Value, 2);
        Assert.Equal(53, observation.HumidityPercent);
        Assert.Equal(1017.57, observation.BarometricPressureMillibars);
        Assert.Equal(0.05, observation.RainLastHourInches!.Value, 2);
        Assert.Equal(0.47, observation.RainLast24HoursInches!.Value, 2);
        Assert.Equal(0.47, observation.RainSinceMidnightInches!.Value, 2);
        Assert.Equal(2.4, observation.UvIndex);
        Assert.Equal(310, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(2, observation.LightningCount);
        Assert.Equal(8.70, observation.LightningDistanceMiles!.Value, 2);
    }

    [Fact]
    public async Task DriverPollsFakeHttpAndPublishesThroughWeatherFramework()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var httpClient = new FakeTempestHttpClient(CurrentObservationJson);
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-token"),
            httpClient);
        manager.RegisterDriver(driver);

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.True(polled);
        Assert.Equal("fake-token", httpClient.LastToken);
        Assert.Contains("/swd/rest/observations/device/12345", httpClient.LastUri!.ToString());

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("12345", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("12345");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.Tempest, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(8, record.WindSpeedMph);
        Assert.Equal(10, record.WindGustMph);
        Assert.Equal(5, record.RainLastHourHundredthsInch);
        Assert.Equal(CurrentObservationJson, record.RawPayload);
    }

    [Fact]
    public void MalformedJson_DoesNotCrash()
    {
        var parser = new TempestCloudJsonParser();

        var result = parser.Parse("{not-json", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("valid JSON", result.Error);
    }

    [Fact]
    public async Task HttpFailure_RecordsErrorSafely()
    {
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-token"),
            new FakeTempestHttpClient(new TimeoutException("timed out")));

        var polled = await driver.PollOnceAsync(ReceivedAt);

        Assert.False(polled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
        Assert.Contains("timed out", driver.LastError!.Message);
    }

    [Fact]
    public void InvalidTokenResponse_IsReturnedAsValidationFailure()
    {
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-token"),
            new FakeTempestHttpClient("""{"status":{"status_code":401,"status_message":"INVALID_TOKEN"}}"""));

        var handled = driver.ProcessPayload("""{"status":{"status_code":401,"status_message":"INVALID_TOKEN"}}""", ReceivedAt);

        Assert.False(handled);
        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.Contains("INVALID_TOKEN", driver.LastError!.Message);
    }

    [Fact]
    public void StaleData_IsMarkedStaleByWeatherDriverManager()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration(),
            new FakeCredentialStore("fake-token"),
            new FakeTempestHttpClient(StaleObservationJson));
        manager.RegisterDriver(driver);

        var handled = driver.ProcessPayload(StaleObservationJson, ReceivedAt);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));

        var record = displayService.GetWeatherStation("12345");
        Assert.NotNull(record);
        Assert.Equal(WeatherDataState.Stale, record.DataState);
    }

    [Fact]
    public async Task StartAsync_MissingTokenDoesNotStartPollingLoop()
    {
        var driver = new TempestCloudWeatherInputDriver(
            EnabledConfiguration() with { AccessTokenReference = "missing-token" },
            NullWeatherCredentialStore.Instance,
            new FakeTempestHttpClient(CurrentObservationJson));

        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.False(driver.LastValidationResult.IsValid);
    }

    private static TempestCloudConfiguration EnabledConfiguration()
    {
        return TempestCloudConfiguration.Default with
        {
            Enabled = true,
            AccessTokenReference = "tempest-token-ref",
            DeviceId = "12345",
            StationId = "67890",
            PollingInterval = TimeSpan.FromMinutes(5)
        };
    }

    private const string CurrentObservationJson =
        """
        {"status":{"status_code":0,"status_message":"SUCCESS"},"device_id":12345,"station_id":67890,"obs":[{"timestamp":1493322445,"wind_avg":3.46,"wind_direction":187,"wind_gust":4.25,"air_temperature":22.37,"relative_humidity":53,"barometric_pressure":1017.57,"precip_accum_last_1hr":1.2,"precip_accum_local_day":12.0,"solar_radiation":310.0,"uv":2.4,"lightning_strike_count":2,"lightning_strike_last_distance":14.0}]}
        """;

    private const string StaleObservationJson =
        """
        {"status":{"status_code":0,"status_message":"SUCCESS"},"device_id":12345,"station_id":67890,"obs":[{"timestamp":1493322445,"wind_avg":3.46,"wind_direction":187,"wind_gust":4.25,"air_temperature":22.37,"relative_humidity":53,"barometric_pressure":1017.57}]}
        """;

    private sealed class FakeCredentialStore : IWeatherCredentialStore
    {
        private readonly string? token;

        public FakeCredentialStore(string? token)
        {
            this.token = token;
        }

        public ValueTask<string?> GetSecretAsync(string credentialReference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(token);
        }
    }

    private sealed class FakeTempestHttpClient : ITempestCloudHttpClient
    {
        private readonly string? response;
        private readonly Exception? exception;

        public FakeTempestHttpClient(string response)
        {
            this.response = response;
        }

        public FakeTempestHttpClient(Exception exception)
        {
            this.exception = exception;
        }

        public Uri? LastUri { get; private set; }

        public string? LastToken { get; private set; }

        public Task<string> GetStringAsync(Uri requestUri, string accessToken, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUri = requestUri;
            LastToken = accessToken;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(response!);
        }
    }
}
