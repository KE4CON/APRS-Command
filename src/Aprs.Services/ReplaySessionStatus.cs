namespace Aprs.Services;

public sealed record ReplaySessionStatus(
    ReplaySessionState State,
    int CurrentIndex,
    int TotalEntries,
    double SpeedMultiplier,
    bool LoopReplay,
    bool TransmitDisabled,
    DateTimeOffset? CurrentOriginalTimestampUtc,
    DateTimeOffset? LastReplayTimestampUtc,
    string? SelectedFilePath,
    string? LastError)
{
    public double ProgressPercent => TotalEntries <= 0
        ? 0
        : Math.Clamp(CurrentIndex * 100.0 / TotalEntries, 0, 100);
}
