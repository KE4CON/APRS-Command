using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Manages AGWPE connections for all enabled AGWPE ports in the operator's Connections settings.
/// AGWPE is the protocol used by AGW Packet Engine and BPQ32. This coordinator connects to the
/// AGWPE host (typically 127.0.0.1:8000 for local BPQ32) and routes received packets into the
/// APRS ingestion pipeline.
/// </summary>
public sealed class AgwpeCoordinator : IAsyncDisposable
{
    private readonly AprsIngestionService ingestion;
    private readonly List<AgwpeClient>    clients = [];
    private readonly CancellationTokenSource cts = new();
    private readonly List<Task>           readLoops = [];

    public AgwpeCoordinator(AprsIngestionService ingestion)
    {
        this.ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
    }

    /// <summary>Creates a coordinator from the persisted connection settings.</summary>
    public static AgwpeCoordinator CreateFromSettings(
        Configuration.AppSettings settings,
        AprsIngestionService ingestion)
    {
        var coordinator = new AgwpeCoordinator(ingestion);

        foreach (var port in settings.Connections.Ports)
        {
            if (port.Type != Configuration.ConnectionPortType.Agwpe) continue;
            if (!port.Enabled || !port.ReceiveEnabled) continue;

            var agwpeConfig = port.Configuration.Agwpe;
            if (agwpeConfig is null || !agwpeConfig.Enabled) continue;

            coordinator.clients.Add(new AgwpeClient(agwpeConfig));
        }

        return coordinator;
    }

    public void Start()
    {
        foreach (var client in clients)
        {
            var loop = RunClientAsync(client, cts.Token);
            readLoops.Add(loop);
        }
    }

    private async Task RunClientAsync(AgwpeClient client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(ct).ConfigureAwait(false);

                await foreach (var args in client.ReadPacketsAsync(ct).ConfigureAwait(false))
                {
                    if (args.RawPacketLine is { Length: > 0 } raw)
                        ingestion.IngestReceivedLine(raw, AprsPacketSource.Agwpe, args.ReceivedAtUtc);
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // Connection error — pause before reconnecting
                try { await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public int ClientCount => clients.Count;

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        foreach (var client in clients)
            await client.DisposeAsync().ConfigureAwait(false);
        cts.Dispose();
    }
}
