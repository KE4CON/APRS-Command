namespace Aprs.Services;

public sealed class DavisWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly DavisWeatherConfiguration davisConfiguration;
    private readonly IWeatherCredentialStore credentialStore;
    private readonly IDavisWeatherLinkHttpClient httpClient;
    private readonly DavisWeatherJsonParser parser;
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;

    public DavisWeatherInputDriver(
        DavisWeatherConfiguration configuration,
        IWeatherCredentialStore? credentialStore = null,
        IDavisWeatherLinkHttpClient? httpClient = null,
        DavisWeatherJsonParser? parser = null)
    {
        davisConfiguration = configuration;
        this.credentialStore = credentialStore ?? NullWeatherCredentialStore.Instance;
        this.httpClient = httpClient ?? new DavisWeatherLinkHttpClient();
        this.parser = parser ?? new DavisWeatherJsonParser();
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
                credentials.Value.ApiKey,
                credentials.Value.ApiSecret,
                davisConfiguration.RequestTimeout,
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
        var result = parser.Parse(rawJson, davisConfiguration, receivedAt);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Davis WeatherLink response could not be handled.");
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
                await Task.Delay(davisConfiguration.PollingInterval, cancellationToken).ConfigureAwait(false);
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
                if (!davisConfiguration.ReconnectEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(davisConfiguration.ReconnectDelay, cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private async Task<bool> CanPollAsync(CancellationToken cancellationToken)
    {
        if (davisConfiguration.DataSourceType != DavisWeatherDataSourceType.WeatherLinkCloudApi)
        {
            SetFailure("Only Davis WeatherLink Cloud API polling is implemented. Local file, local HTTP/IP, and serial logger sources are placeholders for future drivers.");
            return false;
        }

        return await ResolveCredentialsAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private async Task<(string ApiKey, string ApiSecret)?> ResolveCredentialsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(davisConfiguration.StationId))
        {
            SetFailure("Davis WeatherLink station ID is missing.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(davisConfiguration.ApiKeyReference)
            || string.IsNullOrWhiteSpace(davisConfiguration.ApiSecretReference))
        {
            SetFailure("Davis WeatherLink API credential references are missing.");
            return null;
        }

        var apiKey = await credentialStore.GetSecretAsync(davisConfiguration.ApiKeyReference, cancellationToken).ConfigureAwait(false);
        var apiSecret = await credentialStore.GetSecretAsync(davisConfiguration.ApiSecretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            SetFailure("Davis WeatherLink API credentials are missing.");
            return null;
        }

        return (apiKey, apiSecret);
    }

    private Uri BuildRequestUri()
    {
        var baseUri = davisConfiguration.ApiBaseUrl.ToString().TrimEnd('/');
        return new Uri($"{baseUri}/v2/current/{Uri.EscapeDataString(davisConfiguration.StationId ?? string.Empty)}");
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
