namespace Aprs.Services;

public sealed class PeetBrosWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly PeetBrosConfiguration peetConfiguration;
    private readonly IWeatherSerialTextSource? serialTextSource;
    private readonly PeetBrosWeatherParser parser;
    private CancellationTokenSource? receiveCancellation;
    private Task? receiveTask;

    public PeetBrosWeatherInputDriver(
        PeetBrosConfiguration configuration,
        IWeatherSerialTextSource? serialTextSource = null,
        PeetBrosWeatherParser? parser = null)
    {
        peetConfiguration = configuration;
        this.serialTextSource = serialTextSource;
        this.parser = parser ?? new PeetBrosWeatherParser();
        Configuration = configuration.ToDriverConfiguration();
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enabled)
        {
            Status = WeatherInputDriverStatus.Disabled;
            return;
        }

        if (serialTextSource is null)
        {
            SetFailure("Peet Bros serial text source is not configured.");
            return;
        }

        if (receiveTask is { IsCompleted: false })
        {
            return;
        }

        Status = WeatherInputDriverStatus.Starting;
        receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await serialTextSource.OpenAsync(peetConfiguration, receiveCancellation.Token).ConfigureAwait(false);
            Status = WeatherInputDriverStatus.Running;
            receiveTask = Task.Run(() => ReceiveLoopAsync(receiveCancellation.Token), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex;
            LastValidationResult = new WeatherObservationValidationResult(false, [ex.Message], []);
            Status = WeatherInputDriverStatus.Faulted;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        receiveCancellation?.Cancel();

        if (serialTextSource is not null)
        {
            try
            {
                await serialTextSource.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LastError = ex;
            }
        }

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        receiveCancellation?.Dispose();
        receiveCancellation = null;
        receiveTask = null;
        Status = Enabled ? WeatherInputDriverStatus.Stopped : WeatherInputDriverStatus.Disabled;
    }

    public bool ProcessLine(string payload, DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var result = parser.Parse(payload, peetConfiguration, receivedAt);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Peet Bros payload could not be handled.");
            return false;
        }

        LastError = null;
        LastObservation = result.Observation;
        LastValidationResult = new WeatherObservationValidationResult(true, [], []);
        Status = Enabled ? WeatherInputDriverStatus.Running : WeatherInputDriverStatus.Disabled;
        ObservationReceived?.Invoke(this, new WeatherObservationReceivedEventArgs(DriverId, result.Observation, receivedAt));
        return true;
    }

    public void SetValidationResult(WeatherObservationValidationResult validationResult)
    {
        LastValidationResult = validationResult;
    }

    public void SetStatus(WeatherInputDriverStatus status)
    {
        Status = status;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        if (serialTextSource is not null)
        {
            await serialTextSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (serialTextSource is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await serialTextSource.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    await Task.Delay(peetConfiguration.ReadTimeout, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                ProcessLine(line, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex;
                LastValidationResult = new WeatherObservationValidationResult(false, [ex.Message], []);
                Status = WeatherInputDriverStatus.Faulted;
                if (!peetConfiguration.ReconnectEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(peetConfiguration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
