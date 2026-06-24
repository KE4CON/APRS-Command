namespace Aprs.Services;

public sealed record ReplaySessionConfiguration
{
    public bool ReplayEnabled { get; init; }

    public string? SelectedFilePath { get; init; }

    public double SpeedMultiplier { get; init; } = 1.0;

    public bool LoopReplay { get; init; }

    public DateTimeOffset? StartFilterUtc { get; init; }

    public DateTimeOffset? EndFilterUtc { get; init; }

    public bool FeedStationDatabase { get; init; } = true;

    public bool FeedMap { get; init; } = true;

    public bool FeedMessages { get; init; } = true;

    public bool FeedWeather { get; init; } = true;

    public bool FeedObjects { get; init; } = true;

    public bool FeedRawPacketLog { get; init; } = true;

    public bool FeedDecodedEventLog { get; init; } = true;

    public bool TransmitDisabled => true;

    public static ReplaySessionConfiguration Default { get; } = new();
}
