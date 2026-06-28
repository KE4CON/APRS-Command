using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Owns a <see cref="GpsService"/> and one of two GPS sources:
/// (1) a serial NMEA sentence source, or (2) a GPSD TCP client.
/// Runs the background read loop and exposes the latest position.
/// </summary>
public sealed class GpsCoordinator : IAsyncDisposable
{
    private readonly GpsService gpsService;
    private readonly IGpsSentenceSource? serialSource;
    private readonly IGpsdClient? gpsdClient;
    private readonly CancellationTokenSource cts = new();
    private Task? readLoop;

    /// <summary>The underlying GPS service — used by WireGpsStatus to read current position.</summary>
    public GpsService GpsService => gpsService;

    /// <summary>Fired whenever a new valid GPS position is parsed.</summary>
    public event EventHandler<GpsPosition>? PositionUpdated;

    /// <param name="gpsService">The GPS service that processes positions.</param>
    /// <param name="serialSource">Serial NMEA source — used when GPS mode is serial.</param>
    /// <param name="gpsdClient">GPSD TCP client — used when GPS mode is GPSD.</param>
    public GpsCoordinator(GpsService gpsService,
        IGpsSentenceSource? serialSource = null,
        IGpsdClient? gpsdClient = null)
    {
        this.gpsService   = gpsService   ?? throw new ArgumentNullException(nameof(gpsService));
        this.serialSource = serialSource;
        this.gpsdClient   = gpsdClient;
    }

    public GpsPosition? CurrentPosition => gpsService.CurrentPosition;
    public bool HasValidFix             => gpsService.HasValidFix;

    public void Start()
    {
        if (gpsdClient is not null)
            StartGpsd();
        else if (serialSource is not null)
            StartSerial();
    }

    private void StartSerial()
    {
        readLoop = Task.Run(async () =>
        {
            await foreach (var sentence in serialSource!.ReadSentencesAsync(cts.Token)
                                                         .ConfigureAwait(false))
            {
                var result = gpsService.AcceptSentence(sentence);
                if (result.IsParsed && gpsService.CurrentPosition is { } pos)
                    PositionUpdated?.Invoke(this, pos);
            }
        }, cts.Token);
    }

    private void StartGpsd()
    {
        readLoop = Task.Run(async () =>
        {
            await foreach (var pos in gpsdClient!.ReadPositionsAsync(cts.Token)
                                                  .ConfigureAwait(false))
            {
                // GPSD client returns fully-parsed GpsPosition objects.
                // Route them through a synthetic GpsdParseResult so GpsService
                // can update CurrentPosition and HasValidFix correctly.
                var syntheticResult = new GpsdParseResult(
                    IsParsed: true,
                    ReportType: GpsdReportType.Tpv,
                    Position: pos,
                    SatelliteCount: null,
                    UsedSatelliteCount: pos.SatelliteCount,
                    Hdop: null,
                    RawJson: string.Empty,
                    Errors: [],
                    Warnings: []);
                gpsService.AcceptGpsdReport(syntheticResult, "gpsd");
                PositionUpdated?.Invoke(this, pos);
            }
        }, cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        if (readLoop is not null)
        {
            try { await readLoop.ConfigureAwait(false); }
            catch { /* suppress */ }
        }
        if (gpsdClient is not null)
            await gpsdClient.DisposeAsync().ConfigureAwait(false);
        cts.Dispose();
    }
}
