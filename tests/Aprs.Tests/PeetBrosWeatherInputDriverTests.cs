using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class PeetBrosWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsDisabledAndReceiveOnly()
    {
        var configuration = PeetBrosConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal("Peet Bros ULTIMETER", configuration.SourceName);
        Assert.Equal(2400, configuration.BaudRate);
        Assert.Equal(8, configuration.DataBits);
        Assert.Equal(SerialKissParity.None, configuration.Parity);
        Assert.Equal(SerialKissStopBits.One, configuration.StopBits);
        Assert.Contains("Receive-only", configuration.Notes);
    }

    [Fact]
    public void KeyValuePayload_MapsSupportedWeatherFields()
    {
        var parser = new PeetBrosWeatherParser();

        var result = parser.Parse(KeyValuePayload, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal(WeatherObservationSourceType.PeetBrosUltimeter, observation.SourceType);
        Assert.Equal("Peet Bros ULTIMETER", observation.SourceName);
        Assert.Equal("ULTIMETER2100", observation.StationDeviceId);
        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5.5, observation.WindSpeedMph);
        Assert.Equal(10.2, observation.WindGustMph);
        Assert.Equal(72.4, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(1013.2, observation.BarometricPressureMillibars);
        Assert.Equal(0.02, observation.RainLastHourInches);
        Assert.Equal(0.10, observation.RainLast24HoursInches);
        Assert.Equal(0.15, observation.RainSinceMidnightInches);
        Assert.Equal(KeyValuePayload, observation.RawSourcePayload);
        Assert.Equal("key-value", observation.Diagnostics["format"]);
    }

    [Fact]
    public void AprsStyleWeatherText_IsRecognizedAndMapped()
    {
        var parser = new PeetBrosWeatherParser();
        const string payload = "_111111c180s005g010t072r002p010P015h50b10132";

        var result = parser.Parse(payload, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        var observation = result.Observation!;
        Assert.Equal(180, observation.WindDirectionDegrees);
        Assert.Equal(5, observation.WindSpeedMph);
        Assert.Equal(10, observation.WindGustMph);
        Assert.Equal(72, observation.TemperatureFahrenheit);
        Assert.Equal(50, observation.HumidityPercent);
        Assert.Equal(1013.2, observation.BarometricPressureMillibars);
        Assert.Equal(0.02, observation.RainLastHourInches);
        Assert.Equal(0.10, observation.RainLast24HoursInches);
        Assert.Equal(0.15, observation.RainSinceMidnightInches);
        Assert.Equal("aprs-weather", observation.Diagnostics["format"]);
        Assert.Equal(payload, observation.RawSourcePayload);
    }

    [Fact]
    public void MalformedPayload_DoesNotCrash()
    {
        var result = new PeetBrosWeatherParser().Parse("ULTIMETER NATIVE BLOCK TBD", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("not recognized", result.Error);
        Assert.Equal("unknown", result.Diagnostics["format"]);
    }

    [Fact]
    public async Task FakeSerialSource_CanProvideWeatherLine()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var serialSource = new FakeWeatherSerialTextSource([KeyValuePayload]);
        var driver = new PeetBrosWeatherInputDriver(EnabledConfiguration(), serialSource);
        manager.RegisterDriver(driver);

        await driver.StartAsync();
        await WaitForObservationAsync(driver);
        await driver.StopAsync();

        Assert.True(serialSource.Opened);
        Assert.True(serialSource.Closed);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.Equal("ULTIMETER2100", snapshot.LastObservation!.StationDeviceId);

        var record = displayService.GetWeatherStation("ULTIMETER2100");
        Assert.NotNull(record);
        Assert.Equal(WeatherStationSourceType.PeetBros, record.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, record.Origin);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(6, record.WindSpeedMph);
        Assert.Equal(10, record.WindGustMph);
        Assert.Equal(2, record.RainLastHourHundredthsInch);
    }

    [Fact]
    public async Task SerialFailure_RecordsErrorSafely()
    {
        var driver = new PeetBrosWeatherInputDriver(EnabledConfiguration(), new FailingWeatherSerialTextSource());

        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.False(driver.LastValidationResult.IsValid);
    }

    [Fact]
    public void StalePayload_IsMarkedStaleByWeatherDriverManager()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new PeetBrosWeatherInputDriver(EnabledConfiguration());
        manager.RegisterDriver(driver);

        var stalePayload = $"{KeyValuePayload},TS={ReceivedAt.AddMinutes(-30).ToUnixTimeSeconds()}";

        var handled = driver.ProcessLine(stalePayload, ReceivedAt);

        Assert.True(handled);
        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.Equal(WeatherInputDriverStatus.Stale, snapshot.Status);
        Assert.Contains(snapshot.LastValidationResult.Warnings, warning => warning.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WeatherDataState.Stale, displayService.GetWeatherStation("ULTIMETER2100")!.DataState);
    }

    [Fact]
    public void InvalidWeatherValues_AreValidationErrorsNotExceptions()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new PeetBrosWeatherInputDriver(EnabledConfiguration());
        manager.RegisterDriver(driver);

        driver.ProcessLine("MODEL=ULTIMETER2100,WD=999,WS=5,T=72,H=50,B=1013.2", ReceivedAt);

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.False(snapshot.LastValidationResult.IsValid);
        Assert.Contains(snapshot.LastValidationResult.Errors, error => error.Contains("Wind direction", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(displayService.GetAllWeatherStations());
    }

    private static PeetBrosConfiguration EnabledConfiguration()
    {
        return PeetBrosConfiguration.Default with
        {
            Enabled = true,
            SerialPortName = "TEST",
            ModelName = "ULTIMETER2100",
            ReadTimeout = TimeSpan.FromMilliseconds(10)
        };
    }

    private static async Task WaitForObservationAsync(PeetBrosWeatherInputDriver driver)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (driver.LastObservation is null && !timeout.IsCancellationRequested)
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private const string KeyValuePayload = "MODEL=ULTIMETER2100,WD=180,WS=5.5,WG=10.2,T=72.4,H=50,B=1013.2,R1=0.02,R24=0.10,RM=0.15";

    private sealed class FakeWeatherSerialTextSource : IWeatherSerialTextSource
    {
        private readonly Queue<string> lines;

        public FakeWeatherSerialTextSource(IEnumerable<string> lines)
        {
            this.lines = new Queue<string>(lines);
        }

        public bool IsOpen { get; private set; }

        public bool Opened { get; private set; }

        public bool Closed { get; private set; }

        public Task OpenAsync(PeetBrosConfiguration configuration, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Opened = true;
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Closed = true;
            IsOpen = false;
            return Task.CompletedTask;
        }

        public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(lines.Count == 0 ? null : lines.Dequeue());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingWeatherSerialTextSource : IWeatherSerialTextSource
    {
        public bool IsOpen => false;

        public Task OpenAsync(PeetBrosConfiguration configuration, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("serial unavailable");
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
