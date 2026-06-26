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
public sealed class LiveDataCoordinator : IAsyncDisposable
{
    private readonly AprsIngestionService ingestion;
    private readonly IStationDatabase stationDatabase;
    private readonly MapViewModel map;
    private readonly RawPacketLogViewModel rawPacketLog;

    private DispatcherTimer? refreshTimer;
    private AprsIsClient? aprsIsClient;
    private bool dirty = true;

    public LiveDataCoordinator(
        AprsIngestionService ingestion,
        IStationDatabase stationDatabase,
        MapViewModel map,
        RawPacketLogViewModel rawPacketLog)
    {
        this.ingestion = ingestion ?? throw new ArgumentNullException(nameof(ingestion));
        this.stationDatabase = stationDatabase ?? throw new ArgumentNullException(nameof(stationDatabase));
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        this.rawPacketLog = rawPacketLog ?? throw new ArgumentNullException(nameof(rawPacketLog));

        // Ingestion runs on the UI thread (see ConnectAprsIsReceiveOnly), so this is single-threaded.
        this.ingestion.PacketIngested += (_, _) => dirty = true;
    }

    public AprsIsConnectionState ConnectionState => aprsIsClient?.State ?? AprsIsConnectionState.Disconnected;

    /// <summary>Forwarded from the ingestion service — fires after each packet is processed.</summary>
    public event EventHandler? PacketIngested
    {
        add    => ingestion.PacketIngested += value;
        remove => ingestion.PacketIngested -= value;
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
        stationDatabase.UpdateAgeStates(DateTimeOffset.UtcNow);
        map.UpdateStations(stationDatabase.GetVisibleStations());
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

        aprsIsClient = new AprsIsClient(config);
        aprsIsClient.RawPacketReceived += (_, e) =>
            Dispatcher.UIThread.Post(() =>
                ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.AprsIs, e.ReceivedAtUtc));

        // Fire and forget; the client reconnects internally per its configuration.
        _ = aprsIsClient.ConnectAsync(CancellationToken.None);
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
