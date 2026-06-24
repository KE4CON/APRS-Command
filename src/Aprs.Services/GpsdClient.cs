using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Aprs.Services;

public sealed class GpsdClient : IGpsdClient
{
    public const string WatchCommand = "?WATCH={\"enable\":true,\"json\":true}";
    private readonly GpsdConfiguration configuration;
    private readonly IGpsdJsonParser parser;
    private readonly IGpsService gpsService;
    private readonly Func<GpsdConfiguration, CancellationToken, Task<Stream>> streamFactory;
    private Stream? stream;

    public GpsdClient(
        GpsdConfiguration configuration,
        IGpsService? gpsService = null,
        IGpsdJsonParser? parser = null,
        Func<GpsdConfiguration, CancellationToken, Task<Stream>>? streamFactory = null)
    {
        this.configuration = configuration;
        this.gpsService = gpsService ?? new GpsService();
        this.parser = parser ?? new GpsdJsonParser();
        this.streamFactory = streamFactory ?? CreateTcpStreamAsync;
    }

    public event EventHandler<GpsPosition>? PositionReceived;

    public GpsdConnectionState State { get; private set; } = GpsdConnectionState.Disconnected;

    public Exception? LastError { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (!configuration.Enabled)
        {
            State = GpsdConnectionState.Disconnected;
            return;
        }

        State = State == GpsdConnectionState.Faulted ? GpsdConnectionState.Reconnecting : GpsdConnectionState.Connecting;
        try
        {
            stream = await streamFactory(configuration, cancellationToken);
            if (stream.CanTimeout)
            {
                stream.ReadTimeout = (int)configuration.ReadTimeout.TotalMilliseconds;
            }

            await WriteWatchCommandAsync(stream, cancellationToken);
            LastError = null;
            State = GpsdConnectionState.Connected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex;
            State = GpsdConnectionState.Faulted;
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (stream is not null)
        {
            await stream.DisposeAsync();
            stream = null;
        }

        State = GpsdConnectionState.Disconnected;
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<GpsPosition> ReadPositionsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (stream is null || State != GpsdConnectionState.Connected)
            {
                await ConnectAsync(cancellationToken);
                if (State != GpsdConnectionState.Connected)
                {
                    yield break;
                }
            }

            var activeStream = stream;
            if (activeStream is null)
            {
                yield break;
            }

            using var reader = new StreamReader(activeStream, Encoding.ASCII, leaveOpen: true);
            while (!cancellationToken.IsCancellationRequested && State == GpsdConnectionState.Connected)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    LastError = ex;
                    State = GpsdConnectionState.Faulted;
                    break;
                }

                if (line is null)
                {
                    State = GpsdConnectionState.Disconnected;
                    break;
                }

                var result = parser.Parse(line, configuration.SourceName);
                gpsService.AcceptGpsdReport(result, configuration.SourceName);
                if (result.Position is not null)
                {
                    PositionReceived?.Invoke(this, result.Position);
                    yield return result.Position;
                }
            }

            if (!configuration.ReconnectEnabled || cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            State = GpsdConnectionState.Reconnecting;
            await Task.Delay(configuration.ReconnectDelay, cancellationToken);
            stream = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stream is not null)
        {
            await stream.DisposeAsync();
        }
    }

    private static async Task WriteWatchCommandAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes($"{WatchCommand}\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<Stream> CreateTcpStreamAsync(GpsdConfiguration configuration, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(configuration.Host, configuration.Port, cancellationToken);
        return tcpClient.GetStream();
    }
}
