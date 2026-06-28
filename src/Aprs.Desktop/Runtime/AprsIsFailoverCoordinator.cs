using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Monitors the APRS-IS connection and cycles through a list of failover servers
/// when the primary server becomes unavailable. Checks every 60 seconds and
/// switches to the next server after <see cref="DisconnectedThreshold"/> consecutive
/// failed connection attempts.
/// </summary>
public sealed class AprsIsFailoverCoordinator : IAsyncDisposable
{
    private readonly LiveDataCoordinator coordinator;
    private readonly IReadOnlyList<(string Host, int Port)> servers;
    private readonly string callsign;
    private readonly string? filter;
    private readonly CancellationTokenSource cts = new();
    private Task? watchLoop;
    private int currentServerIndex;
    private int disconnectedChecks;

    /// <summary>How many consecutive disconnect checks before switching servers. Default 3 (3 minutes).</summary>
    public int DisconnectedThreshold { get; init; } = 3;

    /// <summary>Check interval. Default 60 seconds.</summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>The hostname of the currently active server.</summary>
    public string ActiveServer => servers[currentServerIndex].Host;

    /// <summary>Fired when the coordinator switches to a new server.</summary>
    public event EventHandler<string>? ServerSwitched;

    public AprsIsFailoverCoordinator(
        LiveDataCoordinator coordinator,
        IReadOnlyList<(string Host, int Port)> servers,
        string callsign,
        string? filter)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.servers     = servers?.Count > 0 ? servers : [("rotate.aprs2.net", 14580)];
        this.callsign    = callsign;
        this.filter      = filter;
    }

    public void Start()
    {
        if (watchLoop is not null) return;

        watchLoop = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CheckInterval, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                var state = coordinator.ConnectionState;

                if (state == AprsIsConnectionState.Connected)
                {
                    disconnectedChecks = 0;
                }
                else
                {
                    disconnectedChecks++;

                    if (disconnectedChecks >= DisconnectedThreshold && servers.Count > 1)
                    {
                        disconnectedChecks = 0;
                        currentServerIndex = (currentServerIndex + 1) % servers.Count;
                        var (host, port)   = servers[currentServerIndex];

                        // Reconnect on the UI thread to avoid threading issues with the coordinator.
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            coordinator.ConnectAprsIsReceiveOnly(callsign, host, port, filter);
                        });

                        ServerSwitched?.Invoke(this, host);
                    }
                }
            }
        }, cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        if (watchLoop is not null)
        {
            try { await watchLoop.ConfigureAwait(false); } catch { }
        }
        cts.Dispose();
    }
}
