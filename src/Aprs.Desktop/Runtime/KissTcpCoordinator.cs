using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Manages KISS-TCP connections for all enabled Network TNC KISS ports in the operator's
/// Connections settings. This is the connection path for GrayWolf, a standalone Direwolf,
/// or any other software that speaks KISS over TCP.
///
/// <para>On startup, creates a <see cref="TcpKissClient"/> for each enabled port,
/// connects it, and routes received packets into the ingestion pipeline. If a connection
/// drops, the client reconnects automatically per its <see cref="TcpKissConfiguration"/>.</para>
/// </summary>
public sealed class KissTcpCoordinator : IAsyncDisposable
{
    private readonly AprsIngestionService ingestion;
    private readonly List<TcpKissClient> clients = [];
    private readonly CancellationTokenSource cts = new();
    private readonly List<Task> readLoops = [];

    public KissTcpCoordinator(AprsIngestionService ingestion)
    {
        this.ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
    }

    /// <summary>
    /// Creates a coordinator from the persisted connection settings. Only ports with type
    /// NetworkTncKiss or ManagedLocalModem that are enabled and have receive enabled are started.
    /// </summary>
    public static KissTcpCoordinator CreateFromSettings(
        Configuration.AppSettings settings,
        AprsIngestionService ingestion)
    {
        var coordinator = new KissTcpCoordinator(ingestion);

        foreach (var port in settings.Connections.Ports)
        {
            var isKiss = port.Type is Configuration.ConnectionPortType.NetworkTncKiss
                                   or Configuration.ConnectionPortType.ManagedLocalModem;
            if (!isKiss || !port.Enabled || !port.ReceiveEnabled) continue;

            var kissConfig = port.Configuration.NetworkTncKiss;
            if (kissConfig is null) continue;

            // Build config — transfer the per-port enabled/transmit flags.
            var config = kissConfig with
            {
                Enabled         = true,
                ReceiveEnabled  = port.ReceiveEnabled,
                TransmitEnabled = port.TransmitEnabled,
                SourceName      = $"Network TNC ({kissConfig.Host}:{kissConfig.Port})"
            };

            coordinator.clients.Add(new TcpKissClient(config));
        }

        return coordinator;
    }

    /// <summary>How many KISS-TCP clients are configured (connected or not).</summary>
    public int ClientCount => clients.Count;

    /// <summary>Starts all clients and begins reading packets into the ingestion pipeline.</summary>
    public void Start()
    {
        foreach (var client in clients)
        {
            // Subscribe to raw packet events.
            client.RawPacketReceived += OnRawPacketReceived;

            // Start connect + read loop on a background thread.
            var c = client; // capture for lambda
            readLoops.Add(Task.Run(async () => await RunClientAsync(c).ConfigureAwait(false), cts.Token));
        }
    }

    private async Task RunClientAsync(TcpKissClient client)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(cts.Token).ConfigureAwait(false);

                // Read frames until disconnected or cancelled.
                await foreach (var _ in client.ReadFramesAsync(cts.Token).ConfigureAwait(false))
                {
                    // Packets arrive via RawPacketReceived event — nothing to do here per frame.
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Connection failed — wait before reconnecting.
                try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void OnRawPacketReceived(object? sender, TcpKissRawPacketReceivedEventArgs e)
    {
        // Feed the decoded APRS packet line into the same ingestion pipeline
        // used by the APRS-IS receive path.
        if (!string.IsNullOrWhiteSpace(e.RawPacketLine))
        {
            ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.TcpKiss, e.ReceivedAtUtc);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);

        foreach (var loop in readLoops)
        {
            try { await loop.ConfigureAwait(false); }
            catch { /* suppress */ }
        }

        foreach (var client in clients)
        {
            client.RawPacketReceived -= OnRawPacketReceived;
            try { await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { /* suppress */ }
            await client.DisposeAsync().ConfigureAwait(false);
        }

        cts.Dispose();
    }
}
