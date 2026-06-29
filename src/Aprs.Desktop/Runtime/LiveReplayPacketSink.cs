using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Routes replayed APRS packets into the live AprsIngestionService pipeline
/// so they appear on the map, station list, and raw packet log tagged as
/// AprsPacketSource.Replay — distinguishable from live traffic.
/// </summary>
public sealed class LiveReplayPacketSink : IReplayPacketSink
{
    private readonly AprsIngestionService ingestionService;

    public LiveReplayPacketSink(AprsIngestionService ingestionService)
        => this.ingestionService = ingestionService
            ?? throw new ArgumentNullException(nameof(ingestionService));

    public Task PublishReplayPacketAsync(
        ReplayPacketDispatch dispatch,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(dispatch.RawPacketText))
        {
            ingestionService.IngestReceivedLine(
                dispatch.RawPacketText,
                AprsPacketSource.Replay,
                dispatch.ReplayTimestampUtc);
        }
        return Task.CompletedTask;
    }
}
