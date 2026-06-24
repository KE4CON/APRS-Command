namespace Aprs.Services;

/// <summary>
/// Loads APRS packet logs and replays them as source-tagged, receive-only packet events.
/// </summary>
public interface IReplayService
{
    ReplaySessionConfiguration Configuration { get; }

    Task<IReadOnlyList<ReplayLogEntry>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    IReadOnlyList<ReplayLogEntry> LoadEntries(IEnumerable<RawPacketLogEntry> rawLogEntries);

    Task StartReplayAsync(CancellationToken cancellationToken = default);

    Task<bool> PlayNextAsync(CancellationToken cancellationToken = default);

    void Pause();

    void Resume();

    void Stop();

    bool SeekToEntryIndex(int entryIndex);

    bool SeekToTimestamp(DateTimeOffset timestampUtc);

    void UpdateConfiguration(ReplaySessionConfiguration configuration);

    ReplaySessionStatus GetStatus();

    IReadOnlyList<ReplayLogEntry> GetEntries();
}
