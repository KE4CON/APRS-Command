using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Monitors the APRS-IS connection state and fires <see cref="ConnectionLost"/> when the
/// connection has been down for longer than <see cref="AlertThreshold"/>. Fires
/// <see cref="ConnectionRestored"/> when the connection comes back.
/// </summary>
public sealed class ConnectionHealthWatchdog : IAsyncDisposable
{
    private readonly LiveDataCoordinator coordinator;
    private readonly CancellationTokenSource cts = new();
    private Task? watchLoop;
    private DateTimeOffset? disconnectedSince;
    private bool alertFired;

    /// <summary>How long the connection must be down before the alert fires. Default 3 minutes.</summary>
    public TimeSpan AlertThreshold { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>Fired when APRS-IS has been disconnected longer than <see cref="AlertThreshold"/>.</summary>
    public event EventHandler? ConnectionLost;

    /// <summary>Fired when APRS-IS reconnects after a lost-connection alert was fired.</summary>
    public event EventHandler? ConnectionRestored;

    public ConnectionHealthWatchdog(LiveDataCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public void Start()
    {
        watchLoop = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                var state = coordinator.ConnectionState;
                var now = DateTimeOffset.UtcNow;

                if (state == AprsIsConnectionState.Connected)
                {
                    if (alertFired)
                    {
                        alertFired = false;
                        disconnectedSince = null;
                        ConnectionRestored?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        disconnectedSince = null;
                    }
                }
                else
                {
                    disconnectedSince ??= now;

                    if (!alertFired && now - disconnectedSince >= AlertThreshold)
                    {
                        alertFired = true;
                        ConnectionLost?.Invoke(this, EventArgs.Empty);
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
            try { await watchLoop.ConfigureAwait(false); }
            catch { }
        }
        cts.Dispose();
    }
}
