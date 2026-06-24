using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherInputDriverManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterDriver_AddsDriver()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        var driver = CreateDriver("manual-1", enabled: true);

        manager.RegisterDriver(driver);

        var snapshot = Assert.Single(manager.GetAllDrivers());
        Assert.Equal("manual-1", snapshot.DriverId);
        Assert.Equal(WeatherInputDriverType.Manual, snapshot.DriverType);
    }

    [Fact]
    public void UnregisterDriver_RemovesDriver()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        manager.RegisterDriver(CreateDriver("manual-1", enabled: true));

        var removed = manager.UnregisterDriver("MANUAL-1");

        Assert.True(removed);
        Assert.Empty(manager.GetAllDrivers());
    }

    [Fact]
    public void GetEnabledDrivers_ReturnsOnlyEnabledDrivers()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        manager.RegisterDriver(CreateDriver("enabled", enabled: true));
        manager.RegisterDriver(CreateDriver("disabled", enabled: false));

        var enabled = manager.GetEnabledDrivers();

        var snapshot = Assert.Single(enabled);
        Assert.Equal("enabled", snapshot.DriverId);
    }

    [Fact]
    public async Task StartEnabledDrivers_DoesNotStartDisabledDrivers()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        var enabled = CreateDriver("enabled", enabled: true);
        var disabled = CreateDriver("disabled", enabled: false);
        manager.RegisterDriver(enabled);
        manager.RegisterDriver(disabled);

        await manager.StartEnabledDriversAsync();

        Assert.Equal(WeatherInputDriverStatus.Running, enabled.Status);
        Assert.Equal(WeatherInputDriverStatus.Disabled, disabled.Status);
    }

    [Fact]
    public async Task StartAndStopDriver_UpdateDriverStatus()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        var driver = CreateDriver("manual-1", enabled: true);
        manager.RegisterDriver(driver);

        Assert.True(await manager.StartDriverAsync("manual-1"));
        Assert.Equal(WeatherInputDriverStatus.Running, manager.GetDriver("manual-1")!.Status);

        Assert.True(await manager.StopDriverAsync("manual-1"));
        Assert.Equal(WeatherInputDriverStatus.Stopped, manager.GetDriver("manual-1")!.Status);
    }

    [Fact]
    public void PublishedObservation_IsValidatedAndForwardedToWeatherDisplayService()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = CreateDriver("manual-1", enabled: true);
        manager.RegisterDriver(driver);

        driver.PublishObservation(CreateObservation(), Now);

        var snapshot = manager.GetDriver("manual-1");
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal(Now, snapshot.LastObservationTimeUtc);
        Assert.Equal("manual-1", snapshot.DriverId);

        var record = displayService.GetWeatherStation("WXLOCAL");
        Assert.NotNull(record);
        Assert.Equal("Local Weather", record.DisplayName);
        Assert.Equal(WeatherStationSourceType.Manual, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal("manual payload", record.RawPayload);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(2, record.RainLastHourHundredthsInch);
    }

    [Fact]
    public void InvalidObservation_StoresValidationErrorAndDoesNotForward()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = CreateDriver("manual-1", enabled: true);
        manager.RegisterDriver(driver);

        driver.PublishObservation(CreateObservation(humidityPercent: 150), Now);

        var snapshot = manager.GetDriver("manual-1");
        Assert.NotNull(snapshot);
        Assert.False(snapshot.LastValidationResult.IsValid);
        Assert.Contains(snapshot.LastValidationResult.Errors, error => error.Contains("Humidity", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(displayService.GetAllWeatherStations());
    }

    [Fact]
    public void StaleObservation_IsMarkedStale()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = CreateDriver("manual-1", enabled: true);
        manager.RegisterDriver(driver);

        driver.PublishObservation(CreateObservation(timestamp: Now.AddMinutes(-30)), Now);

        var snapshot = manager.GetDriver("manual-1");
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));

        var record = displayService.GetWeatherStation("WXLOCAL");
        Assert.NotNull(record);
        Assert.Equal(WeatherDataState.Stale, record.DataState);
        Assert.Equal(TimeSpan.FromMinutes(30), record.DataAge);
    }

    [Fact]
    public async Task StopAllDrivers_StopsEveryDriver()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        var first = CreateDriver("first", enabled: true);
        var second = CreateDriver("second", enabled: true);
        manager.RegisterDriver(first);
        manager.RegisterDriver(second);
        await manager.StartEnabledDriversAsync();

        await manager.StopAllDriversAsync();

        Assert.All(manager.GetAllDrivers(), driver => Assert.Equal(WeatherInputDriverStatus.Stopped, driver.Status));
    }

    [Fact]
    public void RegisterDriver_RejectsDuplicateDriverId()
    {
        var manager = new WeatherInputDriverManager(new WeatherDisplayService());
        manager.RegisterDriver(CreateDriver("manual-1", enabled: true));

        Assert.Throws<InvalidOperationException>(() => manager.RegisterDriver(CreateDriver("MANUAL-1", enabled: true)));
    }

    private static ManualWeatherInputDriver CreateDriver(string driverId, bool enabled)
    {
        return new ManualWeatherInputDriver(new WeatherInputDriverConfiguration(
            driverId,
            "Local Weather",
            WeatherInputDriverType.Manual,
            enabled,
            "Local Weather",
            TimeSpan.FromMinutes(15),
            ReconnectEnabled: true,
            ReconnectDelay: TimeSpan.FromSeconds(30),
            ConnectionTimeout: TimeSpan.FromSeconds(10),
            ReadTimeout: TimeSpan.FromSeconds(10),
            Notes: null));
    }

    private static CommonWeatherObservation CreateObservation(
        DateTimeOffset? timestamp = null,
        int? humidityPercent = 50)
    {
        return new CommonWeatherObservation(
            "Local Weather",
            WeatherObservationSourceType.Manual,
            "WXLOCAL",
            Callsign: null,
            TimestampUtc: timestamp ?? Now,
            Latitude: 39.058333,
            Longitude: -84.508333,
            WindDirectionDegrees: 180,
            WindSpeedMph: 5.4,
            WindGustMph: 10.2,
            TemperatureFahrenheit: 72.4,
            RainLastHourInches: 0.02,
            RainLast24HoursInches: 0.10,
            RainSinceMidnightInches: 0.15,
            humidityPercent,
            BarometricPressureMillibars: 1013.2,
            LuminosityWattsPerSquareMeter: 250,
            UvIndex: 2.5,
            SnowInches: 0,
            LightningCount: 1,
            LightningDistanceMiles: 3.2,
            Diagnostics: new Dictionary<string, string> { ["driver"] = "manual-1" },
            RawSourcePayload: "manual payload",
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);
    }
}
