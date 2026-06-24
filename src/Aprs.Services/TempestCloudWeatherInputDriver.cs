namespace Aprs.Services;

public sealed class TempestCloudWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly TempestCloudConfiguration tempestConfiguration;
    private readonly IWeatherCredentialStore credentialStore;
    private readonly ITempestCloudHttpClient httpClient;
    private readonly TempestCloudJsonParser parser;
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;

    public TempestCloudWeatherInputDriver(
        TempestCloudConfiguration configuration,
        IWeatherCredentialStore? credentialStore = null,
        ITempestCloudHttpClient? httpClient = null,
        TempestCloudJsonParser? parser = null)
    {
        tempestConfiguration = configuration;
        this.credentialStore = credentialStore ?? NullWeatherCredentialStore.Instance;
        this.httpClient = httpClient ?? new TempestCloudHttpClient();
        this.parser = parser ?? new TempestCloudJsonParser();
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

        if (!tempestConfiguration.RestPollingEnabled)
        {
            SetFailure("Tempest Cloud REST polling is disabled.");
            return;
        }

        if (!await CanResolveTokenAsync(cancellationToken).ConfigureAwait(false))
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

        if (!tempestConfiguration.RestPollingEnabled)
        {
            SetFailure("Tempest Cloud REST polling is disabled.");
            return false;
        }

        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            SetFailure("Tempest Cloud access token is missing.");
            return false;
        }

        try
        {
            var rawJson = await httpClient.GetStringAsync(
                BuildRequestUri(),
                token,
                tempestConfiguration.RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            return ProcessPayload(rawJson, receivedAtUtc);
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

    public bool ProcessPayload(string rawJson, DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var result = parser.Parse(rawJson, tempestConfiguration, receivedAt);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Tempest Cloud response could not be handled.");
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
                await Task.Delay(tempestConfiguration.PollingInterval, cancellationToken).ConfigureAwait(false);
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
                if (!tempestConfiguration.ReconnectEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(tempestConfiguration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private async Task<bool> CanResolveTokenAsync(CancellationToken cancellationToken)
    {
        var token = await ResolveTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        SetFailure("Tempest Cloud access token is missing.");
        return false;
    }

    private ValueTask<string?> ResolveTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tempestConfiguration.AccessTokenReference))
        {
            return ValueTask.FromResult<string?>(null);
        }

        return credentialStore.GetSecretAsync(tempestConfiguration.AccessTokenReference, cancellationToken);
    }

    private Uri BuildRequestUri()
    {
        var baseUri = tempestConfiguration.ApiBaseUrl.ToString().TrimEnd('/');
        var path = !string.IsNullOrWhiteSpace(tempestConfiguration.DeviceId)
            ? $"/swd/rest/observations/device/{Uri.EscapeDataString(tempestConfiguration.DeviceId)}"
            : $"/swd/rest/observations/station/{Uri.EscapeDataString(tempestConfiguration.StationId ?? string.Empty)}";
        return new Uri($"{baseUri}{path}");
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
