namespace Aprs.Services;

public sealed class EcowittWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly EcowittWeatherConfiguration ecowittConfiguration;
    private readonly IEcowittWeatherHttpClient httpClient;
    private readonly EcowittWeatherPayloadParser parser;
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;

    public EcowittWeatherInputDriver(
        EcowittWeatherConfiguration configuration,
        IEcowittWeatherHttpClient? httpClient = null,
        EcowittWeatherPayloadParser? parser = null)
    {
        ecowittConfiguration = configuration;
        this.httpClient = httpClient ?? new EcowittWeatherHttpClient();
        this.parser = parser ?? new EcowittWeatherPayloadParser();
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

        if (!CanPoll())
        {
            return;
        }

        if (pollTask is { IsCompleted: false })
        {
            return;
        }

        Status = WeatherInputDriverStatus.Running;
        pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pollTask = Task.Run(() => PollLoopAsync(pollCancellation.Token), CancellationToken.None);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        pollCancellation?.Cancel();
        if (pollTask is not null)
        {
            try
            {
                await pollTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        pollCancellation?.Dispose();
        pollCancellation = null;
        pollTask = null;
        Status = Enabled ? WeatherInputDriverStatus.Stopped : WeatherInputDriverStatus.Disabled;
    }

    public async Task<bool> PollOnceAsync(DateTimeOffset? receivedAtUtc = null, CancellationToken cancellationToken = default)
    {
        if (!Enabled)
        {
            Status = WeatherInputDriverStatus.Disabled;
            return false;
        }

        if (!CanPoll())
        {
            return false;
        }

        try
        {
            var rawPayload = await httpClient.GetStringAsync(
                BuildRequestUri(),
                ecowittConfiguration.RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            return ProcessPayload(rawPayload, receivedAtUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex;
            LastValidationResult = new WeatherObservationValidationResult(false, [ex.Message], []);
            Status = WeatherInputDriverStatus.Faulted;
            return false;
        }
    }

    public bool ProcessPayload(string rawPayload, DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var result = parser.Parse(rawPayload, ecowittConfiguration, receivedAt);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Ecowitt/Fine Offset payload could not be handled.");
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
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                await Task.Delay(ecowittConfiguration.PollingInterval, cancellationToken).ConfigureAwait(false);
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
                if (!ecowittConfiguration.ReconnectEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(ecowittConfiguration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private bool CanPoll()
    {
        if (ecowittConfiguration.DataSourceType != EcowittWeatherDataSourceType.LocalGatewayHttpPolling
            && ecowittConfiguration.DataSourceType != EcowittWeatherDataSourceType.SimulationTestPayload)
        {
            SetFailure("Only Ecowitt/Fine Offset local gateway polling is implemented. Custom upload receiver and file import sources are placeholders for future local API/import work.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ecowittConfiguration.GatewayHost))
        {
            SetFailure("Ecowitt/Fine Offset gateway host is missing.");
            return false;
        }

        if (ecowittConfiguration.GatewayPort is <= 0 or > 65535)
        {
            SetFailure("Ecowitt/Fine Offset gateway port must be between 1 and 65535.");
            return false;
        }

        return true;
    }

    private Uri BuildRequestUri()
    {
        var path = string.IsNullOrWhiteSpace(ecowittConfiguration.ApiPath) ? "/" : ecowittConfiguration.ApiPath;
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return new Uri($"http://{ecowittConfiguration.GatewayHost}:{ecowittConfiguration.GatewayPort}{path}");
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
