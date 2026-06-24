namespace Aprs.Services;

public sealed class ManualWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink
{
    public ManualWeatherInputDriver(WeatherInputDriverConfiguration configuration)
    {
        Configuration = configuration;
        Status = configuration.Enabled ? WeatherInputDriverStatus.Stopped : WeatherInputDriverStatus.Disabled;
        LastValidationResult = new WeatherObservationValidationResult(true, [], []);
    }

    public string DriverId => Configuration.DriverId;

    public string DriverName => Configuration.DriverName;

    public WeatherInputDriverType DriverType => Configuration.DriverType;

    public bool Enabled => Configuration.Enabled;

    public WeatherInputDriverStatus Status { get; private set; }

    public CommonWeatherObservation? LastObservation { get; private set; }

    public Exception? LastError { get; private set; }

    public WeatherObservationValidationResult LastValidationResult { get; private set; }

    public WeatherInputDriverConfiguration Configuration { get; }

    public event EventHandler<WeatherObservationReceivedEventArgs>? ObservationReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Status = Enabled ? WeatherInputDriverStatus.Running : WeatherInputDriverStatus.Disabled;
        LastError = null;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Status = Enabled ? WeatherInputDriverStatus.Stopped : WeatherInputDriverStatus.Disabled;
        return Task.CompletedTask;
    }

    public void PublishObservation(CommonWeatherObservation observation, DateTimeOffset? receivedAtUtc = null)
    {
        LastObservation = observation;
        ObservationReceived?.Invoke(
            this,
            new WeatherObservationReceivedEventArgs(DriverId, observation, receivedAtUtc ?? DateTimeOffset.UtcNow));
    }

    public void SetValidationResult(WeatherObservationValidationResult validationResult)
    {
        LastValidationResult = validationResult;
    }

    public void SetStatus(WeatherInputDriverStatus status)
    {
        Status = status;
    }

    internal void SetLastError(Exception? lastError)
    {
        LastError = lastError;
    }
}
