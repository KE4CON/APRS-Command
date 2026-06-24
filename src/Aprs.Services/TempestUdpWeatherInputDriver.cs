using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aprs.Services;

public sealed class TempestUdpWeatherInputDriver : IWeatherInputDriver, IWeatherInputDriverStatusSink, IAsyncDisposable
{
    private readonly TempestUdpConfiguration tempestConfiguration;
    private readonly TempestUdpJsonParser parser;
    private CancellationTokenSource? receiveCancellation;
    private Task? receiveTask;
    private UdpClient? udpClient;

    public TempestUdpWeatherInputDriver(TempestUdpConfiguration configuration, TempestUdpJsonParser? parser = null)
    {
        tempestConfiguration = configuration;
        this.parser = parser ?? new TempestUdpJsonParser();
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

        if (receiveTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        Status = WeatherInputDriverStatus.Starting;
        receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        udpClient = CreateUdpClient();
        Status = WeatherInputDriverStatus.Running;
        receiveTask = Task.Run(() => ReceiveLoopAsync(receiveCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        receiveCancellation?.Cancel();
        udpClient?.Close();

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

        udpClient?.Dispose();
        udpClient = null;
        receiveCancellation?.Dispose();
        receiveCancellation = null;
        receiveTask = null;
        Status = Enabled ? WeatherInputDriverStatus.Stopped : WeatherInputDriverStatus.Disabled;
    }

    public void ProcessPayload(string rawJson, DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var result = parser.Parse(rawJson, tempestConfiguration, receivedAt);
        if (!result.IsHandled)
        {
            LastError = new InvalidOperationException(result.Error ?? "Tempest UDP payload could not be handled.");
            LastValidationResult = new WeatherObservationValidationResult(false, [LastError.Message], []);
            Status = Enabled ? WeatherInputDriverStatus.Faulted : WeatherInputDriverStatus.Disabled;
            return;
        }

        LastError = null;
        LastValidationResult = new WeatherObservationValidationResult(true, [], []);

        if (result.Observation is null)
        {
            Status = Enabled ? WeatherInputDriverStatus.Running : WeatherInputDriverStatus.Disabled;
            return;
        }

        LastObservation = result.Observation;
        Status = Enabled ? WeatherInputDriverStatus.Running : WeatherInputDriverStatus.Disabled;
        ObservationReceived?.Invoke(this, new WeatherObservationReceivedEventArgs(DriverId, result.Observation, receivedAt));
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

    private UdpClient CreateUdpClient()
    {
        var address = tempestConfiguration.BindAddress.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Any
            : IPAddress.Parse(tempestConfiguration.BindAddress);
        var client = new UdpClient(new IPEndPoint(address, tempestConfiguration.ListenPort))
        {
            Client =
            {
                ReceiveTimeout = (int)Math.Max(1, tempestConfiguration.ReceiveTimeout.TotalMilliseconds)
            }
        };
        return client;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (udpClient is null)
                {
                    Status = WeatherInputDriverStatus.Disconnected;
                    return;
                }

                var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                var rawJson = Encoding.UTF8.GetString(result.Buffer);
                ProcessPayload(rawJson, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = WeatherInputDriverStatus.Faulted;
                if (!tempestConfiguration.RestartEnabled)
                {
                    break;
                }

                Status = WeatherInputDriverStatus.Reconnecting;
                try
                {
                    await Task.Delay(tempestConfiguration.RestartDelay, cancellationToken).ConfigureAwait(false);
                    Status = WeatherInputDriverStatus.Running;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
