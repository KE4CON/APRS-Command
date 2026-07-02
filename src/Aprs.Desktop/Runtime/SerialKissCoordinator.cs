using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Manages Serial KISS connections for all enabled hardware TNC ports in the operator's
/// Connections settings. This is the connection path for any hardware TNC connected via
/// a USB-serial or RS-232 serial port — Kantronics KPC-3+, Kenwood TM-D710, TNC-Pi,
/// Mobilinkd, and any other device that speaks KISS over a serial port.
///
/// <para>On startup, creates a <see cref="SerialKissClient"/> for each enabled Serial KISS
/// port, connects it, and routes received packets into the ingestion pipeline. If a
/// connection drops, the client reconnects automatically.</para>
/// </summary>
public sealed class SerialKissCoordinator : IAsyncDisposable
{
    private readonly AprsIngestionService ingestion;
    private readonly List<SerialKissClient> clients = [];
    private readonly CancellationTokenSource cts = new();
    private readonly List<Task> readLoops = [];

    public SerialKissCoordinator(AprsIngestionService ingestion)
    {
        this.ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
    }

    /// <summary>
    /// Creates a coordinator from the persisted connection settings.
    /// Only ports with type SerialKiss that are enabled and have receive enabled are started.
    /// </summary>
    public static SerialKissCoordinator CreateFromSettings(
        Configuration.AppSettings settings,
        AprsIngestionService ingestion)
    {
        var coordinator = new SerialKissCoordinator(ingestion);
        var factory     = SystemSerialPortConnectionFactory.Instance;

        foreach (var port in settings.Connections.Ports)
        {
            if (port.Type != Configuration.ConnectionPortType.SerialKiss) continue;
            if (!port.Enabled || !port.ReceiveEnabled) continue;

            var serialConfig = port.Configuration.SerialKiss;
            if (serialConfig is null) continue;

            if (string.IsNullOrWhiteSpace(serialConfig.PortName)) continue;

            var config = serialConfig with
            {
                Enabled         = true,
                ReceiveEnabled  = port.ReceiveEnabled,
                TransmitEnabled = port.TransmitEnabled,
                SourceName      = $"Serial KISS ({serialConfig.PortName})"
            };

            coordinator.clients.Add(new SerialKissClient(config, factory));
        }

        return coordinator;
    }

    /// <summary>How many Serial KISS clients are configured.</summary>
    public int ClientCount => clients.Count;

    /// <summary>Returns clients that are connected and have transmit enabled.</summary>
    public IReadOnlyList<SerialKissClient> GetTransmitClients()
        => clients.Where(c => c.Configuration.TransmitEnabled).ToList();

    /// <summary>Starts all clients and begins reading packets into the ingestion pipeline.</summary>
    public void Start()
    {
        foreach (var client in clients)
        {
            client.RawPacketReceived += OnRawPacketReceived;

            var c = client;
            readLoops.Add(Task.Run(async () => await RunClientAsync(c).ConfigureAwait(false), cts.Token));
        }
    }

    private async Task RunClientAsync(SerialKissClient client)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(cts.Token).ConfigureAwait(false);

                await foreach (var _ in client.ReadFramesAsync(cts.Token).ConfigureAwait(false))
                {
                    // Packets arrive via RawPacketReceived event — nothing to do per frame.
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Port error (unplugged, permissions, busy) — wait before retrying.
                try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void OnRawPacketReceived(object? sender, TcpKissRawPacketReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.RawPacketLine))
        {
            ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.SerialKiss, e.ReceivedAtUtc);
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
