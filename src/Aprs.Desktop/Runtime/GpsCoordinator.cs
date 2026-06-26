using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Owns a <see cref="GpsService"/> and a sentence source, runs the background read loop,
/// and exposes the latest position. Used by <see cref="DesktopRuntime"/> to power the GPS
/// status view and optionally update the station position from live GPS data.
/// </summary>
public sealed class GpsCoordinator : IAsyncDisposable
{
    private readonly GpsService gpsService;
    private readonly IGpsSentenceSource? source;
    private readonly CancellationTokenSource cts = new();
    private Task? readLoop;

    /// <summary>Fired whenever a new valid GPS position is parsed.</summary>
    public event EventHandler<GpsPosition>? PositionUpdated;

    public GpsCoordinator(GpsService gpsService, IGpsSentenceSource? source)
    {
        this.gpsService = gpsService ?? throw new ArgumentNullException(nameof(gpsService));
        this.source = source;
    }

    public GpsPosition? CurrentPosition => gpsService.CurrentPosition;
    public bool HasValidFix => gpsService.HasValidFix;

    public void Start()
    {
        if (source is null) return;

        readLoop = Task.Run(async () =>
        {
            await foreach (var sentence in source.ReadSentencesAsync(cts.Token).ConfigureAwait(false))
            {
                var result = gpsService.AcceptSentence(sentence);
                if (result.IsParsed && gpsService.CurrentPosition is { } pos)
                {
                    PositionUpdated?.Invoke(this, pos);
                }
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
        cts.Dispose();
    }
}
