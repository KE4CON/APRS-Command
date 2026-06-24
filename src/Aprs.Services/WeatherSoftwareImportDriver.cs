using System.Text;

namespace Aprs.Services;

public sealed class WeatherSoftwareImportDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly WeatherSoftwareImportConfiguration importConfiguration;
    private readonly IWeatherSoftwareHttpClient httpClient;
    private readonly WeatherSoftwareImportParser parser;
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;

    public WeatherSoftwareImportDriver(
        WeatherSoftwareImportConfiguration configuration,
        IWeatherSoftwareHttpClient? httpClient = null,
        WeatherSoftwareImportParser? parser = null)
    {
        importConfiguration = configuration;
        this.httpClient = httpClient ?? new WeatherSoftwareHttpClient();
        this.parser = parser ?? new WeatherSoftwareImportParser();
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

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enabled)
        {
            Status = WeatherInputDriverStatus.Disabled;
            return Task.CompletedTask;
        }

        if (!CanPoll())
        {
            return Task.CompletedTask;
        }

        if (pollTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        Status = WeatherInputDriverStatus.Running;
        pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        pollTask = Task.Run(() => PollLoopAsync(pollCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
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
            var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
            var (payload, lastWriteUtc) = importConfiguration.SoftwareType == WeatherSoftwareType.LocalHttpEndpoint
                ? (await httpClient.GetStringAsync(importConfiguration.LocalHttpUrl!, importConfiguration.ReadTimeout, cancellationToken).ConfigureAwait(false), (DateTimeOffset?)null)
                : (await ReadFileAsync(cancellationToken).ConfigureAwait(false), File.GetLastWriteTimeUtc(importConfiguration.FilePath!));
            return ProcessPayload(payload, receivedAt, lastWriteUtc);
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

    public bool ProcessPayload(string payload, DateTimeOffset? receivedAtUtc = null, DateTimeOffset? fileLastWriteUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var result = parser.Parse(payload, importConfiguration, receivedAt, fileLastWriteUtc);
        if (!result.IsHandled || result.Observation is null)
        {
            SetFailure(result.Error ?? "Weather software payload could not be handled.");
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
                await Task.Delay(importConfiguration.PollingInterval, cancellationToken).ConfigureAwait(false);
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
                if (!importConfiguration.RetryEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                Status = WeatherInputDriverStatus.Running;
            }
        }
    }

    private bool CanPoll()
    {
        if (importConfiguration.SoftwareType == WeatherSoftwareType.LocalHttpEndpoint)
        {
            if (importConfiguration.LocalHttpUrl is null)
            {
                SetFailure("Weather software local HTTP URL is missing.");
                return false;
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(importConfiguration.FilePath))
        {
            SetFailure("Weather software import file path is missing.");
            return false;
        }

        if (!File.Exists(importConfiguration.FilePath))
        {
            SetFailure("Weather software import file does not exist.");
            return false;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(importConfiguration.FilePath);
        if (DateTimeOffset.UtcNow - lastWriteUtc > importConfiguration.FileStaleThreshold)
        {
            Status = WeatherInputDriverStatus.Stale;
        }

        return true;
    }

    private async Task<string> ReadFileAsync(CancellationToken cancellationToken)
    {
        var encoding = Encoding.GetEncoding(importConfiguration.EncodingName);
        using var stream = new FileStream(
            importConfiguration.FilePath!,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(importConfiguration.ReadTimeout);
        return await reader.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
    }

    private void SetFailure(string message)
    {
        LastError = new InvalidOperationException(message);
        LastValidationResult = new WeatherObservationValidationResult(false, [message], []);
        Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
    }
}
