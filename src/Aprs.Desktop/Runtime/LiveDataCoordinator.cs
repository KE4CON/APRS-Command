using System.Threading;
using Avalonia.Threading;
using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Bridges the background receive pipeline to the live view models on the UI thread.
/// Subscribes to ingestion, coalesces refreshes on a timer to avoid UI thrash under a
/// busy feed, and can open a receive-only APRS-IS connection.
/// </summary>
public sealed class LiveDataCoordinator : IAsyncDisposable, IReplayMapController
{
    private readonly AprsIngestionService ingestion;
    private readonly IStationDatabase stationDatabase;
    private readonly IStationDatabase replayStationDatabase;
    private readonly MapViewModel map;
    private readonly RawPacketLogViewModel rawPacketLog;

    private DispatcherTimer? refreshTimer;
    private AprsIsClient? aprsIsClient;
    private bool dirty = true;
    private bool replayMode;

    public LiveDataCoordinator(
        AprsIngestionService ingestion,
        IStationDatabase stationDatabase,
        MapViewModel map,
        RawPacketLogViewModel rawPacketLog,
        IStationDatabase replayStationDatabase)
    {
        this.ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        this.stationDatabase = stationDatabase ?? throw new ArgumentNullException(nameof(stationDatabase));
        this.replayStationDatabase = replayStationDatabase ?? throw new ArgumentNullException(nameof(replayStationDatabase));
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        this.rawPacketLog = rawPacketLog ?? throw new ArgumentNullException(nameof(rawPacketLog));

        // Ingestion runs on the UI thread (see ConnectAprsIsReceiveOnly), so this is single-threaded.
        this.ingestion.PacketIngested += (_, _) => dirty = true;
    }

    /// <summary>The station database currently feeding the map: replay set in replay mode, else live.</summary>
    private IStationDatabase ActiveStationDatabase => replayMode ? replayStationDatabase : stationDatabase;

    /// <inheritdoc />
    public bool IsReplayMode => replayMode;

    /// <inheritdoc />
    public void EnterReplayMode()
    {
        // Start from a clean replay map. Live keeps ingesting into its own database underneath.
        replayStationDatabase.Clear();
        replayStationDatabase.ClearAllTrails();
        replayMode = true;
        dirty = true;
        RefreshIfDirty();
    }

    /// <inheritdoc />
    public void ExitReplayMode()
    {
        replayMode = false;
        // Drop the replay set now that we're back to live; the live database already holds
        // everything that arrived while the replay was playing.
        replayStationDatabase.Clear();
        replayStationDatabase.ClearAllTrails();
        dirty = true;
        RefreshIfDirty();
    }

    public AprsIsConnectionState ConnectionState => aprsIsClient?.State ?? AprsIsConnectionState.Disconnected;

    /// <summary>Forwarded from the ingestion service — fires after each packet is processed.</summary>
    public event EventHandler? PacketIngested
    {
        add    => ingestion.PacketIngested += value;
        remove => ingestion.PacketIngested -= value;
    }

    /// <summary>Forwarded from the ingestion service — fires with the parsed packet and source (packet is null if unparseable).</summary>
    public event EventHandler<ParsedPacketEventArgs>? PacketParsed
    {
        add    => ingestion.PacketParsed += value;
        remove => ingestion.PacketParsed -= value;
    }

    /// <summary>Starts the coalesced UI refresh loop.</summary>
    public void Start()
    {
        refreshTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.Background,
            (_, _) => RefreshIfDirty());
        refreshTimer.Start();
    }

    private void RefreshIfDirty()
    {
        if (!dirty)
        {
            return;
        }

        dirty = false;
        var source = ActiveStationDatabase;
        source.UpdateAgeStates(DateTimeOffset.UtcNow);
        map.UpdateStations(source.GetVisibleStations());
        rawPacketLog.Refresh();
    }

    /// <summary>
    /// Opens a receive-only APRS-IS connection. Receive-only uses passcode "-1" and never
    /// transmits. Incoming lines are marshalled to the UI thread and ingested there so all
    /// station-database and log access stays single-threaded.
    /// </summary>
    public void ConnectAprsIsReceiveOnly(string callsign, string? serverHost = null, int? serverPort = null, string? filter = null)
    {
        var defaults = AprsIsClientConfiguration.Default;
        var config = defaults with
        {
            Callsign = string.IsNullOrWhiteSpace(callsign) ? "N0CALL" : callsign.Trim(),
            ServerHost = string.IsNullOrWhiteSpace(serverHost) ? defaults.ServerHost : serverHost!.Trim(),
            ServerPort = serverPort ?? defaults.ServerPort,
            // A null/empty filter logs in to the full feed; pass "r/<lat>/<lon>/<km>" to limit
            // reception to an area of interest, or a custom filter string from the port config.
            Filter = string.IsNullOrWhiteSpace(filter) ? defaults.Filter : filter!.Trim(),
            ReceiveOnly = true,
            TransmitEnabled = false,
        };

        // Tear down any prior client (the failover coordinator calls this again on each server switch).
        // Previously the field was overwritten without disposing the old client, leaking its socket +
        // receive-loop task + CancellationTokenSource — and, with reconnect enabled, the old client kept
        // reconnecting and double-ingesting if the old server recovered (audit deep pass).
        var previous = aprsIsClient;

        var client = new AprsIsClient(config);
        client.RawPacketReceived += (_, e) =>
            Dispatcher.UIThread.Post(() =>
                ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.AprsIs, e.ReceivedAtUtc));
        aprsIsClient = client;

        // Fire and forget; the client reconnects internally per its configuration.
        _ = client.ConnectAsync(CancellationToken.None);

        if (previous is not null)
        {
            _ = DisposePreviousReceiveClientAsync(previous);
        }
    }

    private static async Task DisposePreviousReceiveClientAsync(IAprsIsClient client)
    {
        try { await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* best effort */ }
        try { await client.DisposeAsync().ConfigureAwait(false); } catch { /* best effort */ }
    }

    public async ValueTask DisposeAsync()
    {
        refreshTimer?.Stop();

        if (aprsIsClient is not null)
        {
            try
            {
                await aprsIsClient.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ignore shutdown errors.
            }

            await aprsIsClient.DisposeAsync().ConfigureAwait(false);
        }
    }
}
