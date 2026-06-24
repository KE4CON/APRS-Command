namespace Aprs.Services;

public sealed class AmbientWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly AmbientWeatherConfiguration ambientConfiguration;
    private readonly IWeatherCredentialStore credentialStore;
    private readonly IAmbientWeatherHttpClient httpClient;
    private readonly AmbientWeatherJsonParser parser;
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;

    public AmbientWeatherInputDriver(
        AmbientWeatherConfiguration configuration,
        IWeatherCredentialStore? credentialStore = null,
        IAmbientWeatherHttpClient? httpClient = null,
        AmbientWeatherJsonParser? parser = null)
    {
        ambientConfiguration = configuration;
        this.credentialStore = credentialStore ?? NullWeatherCredentialStore.Instance;
        this.httpClient = httpClient ?? new AmbientWeatherHttpClient();
        this.parser = parser ?? new AmbientWeatherJsonParser();
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

        if (!await CanPollAsync(cancellationToken).ConfigureAwait(false))
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

        var credentials = await ResolveCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            return false;
        }

        try
        {
            var rawJson = await httpClient.GetStringAsync(
                BuildRequestUri(),
                credentials.Value.ApplicationKey,
                credentials.Value.ApiKey,
                ambientConfiguration.RequestTimeout,
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
        var result = parser.Parse(rawJson, ambientConfiguration, receivedAt);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Ambient Weather response could not be handled.");
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
                await Task.Delay(ambientConfiguration.PollingInterval, cancellationToken).ConfigureAwait(false);
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
                if (!ambientConfiguration.ReconnectEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(ambientConfiguration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private async Task<bool> CanPollAsync(CancellationToken cancellationToken)
    {
        if (ambientConfiguration.DataSourceType != AmbientWeatherDataSourceType.AmbientWeatherApi)
        {
            SetFailure("Only Ambient Weather API polling is implemented. Local network receiver and file import sources are placeholders for future drivers.");
            return false;
        }

        return await ResolveCredentialsAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private async Task<(string ApplicationKey, string ApiKey)?> ResolveCredentialsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ambientConfiguration.ApplicationKeyReference)
            || string.IsNullOrWhiteSpace(ambientConfiguration.ApiKeyReference))
        {
            SetFailure("Ambient Weather API credential references are missing.");
            return null;
        }

        var applicationKey = await credentialStore.GetSecretAsync(ambientConfiguration.ApplicationKeyReference, cancellationToken).ConfigureAwait(false);
        var apiKey = await credentialStore.GetSecretAsync(ambientConfiguration.ApiKeyReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(applicationKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            SetFailure("Ambient Weather API credentials are missing.");
            return null;
        }

        return (applicationKey, apiKey);
    }

    private Uri BuildRequestUri()
    {
        var baseUri = ambientConfiguration.ApiBaseUrl.ToString().TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(ambientConfiguration.DeviceId)
            ? "/v1/devices"
            : $"/v1/devices/{Uri.EscapeDataString(ambientConfiguration.DeviceId)}";
        return new Uri($"{baseUri}{path}");
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
