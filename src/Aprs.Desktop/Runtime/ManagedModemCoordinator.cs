using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Orchestrates the managed local modem: launches Direwolf via
/// <see cref="DirewolfProcessManager"/>, waits for it to be ready,
/// then connects via loopback KISS-TCP and feeds packets into the
/// ingestion pipeline.
/// </summary>
public sealed class ManagedModemCoordinator : IAsyncDisposable
{
    private readonly DirewolfProcessManager processManager;
    private readonly AprsIngestionService ingestion;
    private readonly Configuration.ManagedModemSettings settings;
    private TcpKissClient? kissClient;
    private readonly CancellationTokenSource cts = new();
    private Task? kissLoop;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<DirewolfProcessState>? StateChanged;

    public DirewolfProcessState State => processManager.State;
    public string? FailureReason => processManager.FailureReason;

    public ManagedModemCoordinator(
        Configuration.ManagedModemSettings settings,
        string callsign,
        AprsIngestionService ingestion)
    {
        this.settings = settings;
        this.ingestion = ingestion;
        processManager = new DirewolfProcessManager(settings, callsign);
        processManager.OutputReceived += (_, line) => OutputReceived?.Invoke(this, line);
        processManager.StateChanged   += OnProcessStateChanged;
    }

    public static ManagedModemCoordinator? CreateIfEnabled(
        Configuration.AppSettings appSettings,
        AprsIngestionService ingestion)
    {
        var s = appSettings.ManagedModem;
        if (!s.Enabled) return null;

        var callsign = appSettings.Station.Callsign;
        if (string.IsNullOrWhiteSpace(callsign) || callsign.Equals("N0CALL", StringComparison.OrdinalIgnoreCase))
            return null;

        return new ManagedModemCoordinator(s, callsign, ingestion);
    }

    public void Start() => processManager.Start();

    private void OnProcessStateChanged(object? sender, DirewolfProcessState state)
    {
        StateChanged?.Invoke(this, state);

        if (state == DirewolfProcessState.Running)
        {
            // Give Direwolf a moment to open its KISS TCP port, then connect.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token).ConfigureAwait(false);
                await ConnectKissAsync().ConfigureAwait(false);
            }, cts.Token);
        }
        else if (state is DirewolfProcessState.Failed or DirewolfProcessState.Stopped)
        {
            // Disconnect KISS if Direwolf stopped.
            _ = DisconnectKissAsync();
        }
    }

    private async Task ConnectKissAsync()
    {
        try
        {
            await DisconnectKissAsync().ConfigureAwait(false);

            var config = TcpKissConfiguration.Default with
            {
                Host            = "127.0.0.1",
                Port            = settings.KissPort,
                Enabled         = true,
                ReceiveEnabled  = true,
                TransmitEnabled = settings.TransmitEnabled,
                SourceName      = "Managed Modem (Direwolf)",
                ReconnectEnabled = false // process manager handles restart
            };

            kissClient = new TcpKissClient(config);
            kissClient.RawPacketReceived += OnRawPacketReceived;

            OutputReceived?.Invoke(this, $"[Managed Modem] Connecting KISS-TCP on 127.0.0.1:{settings.KissPort}");
            await kissClient.ConnectAsync(cts.Token).ConfigureAwait(false);
            OutputReceived?.Invoke(this, "[Managed Modem] KISS-TCP connected. Receiving packets.");

            // Start read loop.
            kissLoop = Task.Run(async () =>
            {
                try
                {
                    await foreach (var _ in kissClient.ReadFramesAsync(cts.Token).ConfigureAwait(false)) { }
                }
                catch { /* suppress on disconnect */ }
            }, cts.Token);
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke(this, $"[Managed Modem] KISS-TCP connection failed: {ex.Message}");
        }
    }

    private async Task DisconnectKissAsync()
    {
        if (kissClient is null) return;
        var c = kissClient;
        kissClient = null;
        c.RawPacketReceived -= OnRawPacketReceived;
        try { await c.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
        await c.DisposeAsync().ConfigureAwait(false);
        if (kissLoop is not null)
        {
            try { await kissLoop.ConfigureAwait(false); } catch { }
            kissLoop = null;
        }
    }

    private void OnRawPacketReceived(object? sender, TcpKissRawPacketReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.RawPacketLine))
            ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.Direwolf, e.ReceivedAtUtc);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        await DisconnectKissAsync().ConfigureAwait(false);
        await processManager.DisposeAsync().ConfigureAwait(false);
        cts.Dispose();
    }
}
