using Aprs.Services;

namespace AprsCommand.Api;

public sealed record WebSocketEventStreamConfiguration
{
    public bool WebSocketEnabled { get; init; }
    public string BindAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8766;
    public bool LocalhostOnly { get; init; } = true;
    public bool RequireToken { get; init; } = true;
    public string? ApiTokenReference { get; init; }
    public int MaximumConnectedClients { get; init; } = 8;
    public int MaximumEventsPerSecondPerClient { get; init; } = 20;
    public IReadOnlySet<AprsEventCategory> AllowedEventCategories { get; init; } = Enum.GetValues<AprsEventCategory>().ToHashSet();
    public IReadOnlySet<AprsEventType> AllowedEventTypes { get; init; } = Enum.GetValues<AprsEventType>().ToHashSet();
    public bool IncludeRawPackets { get; init; } = true;
    public bool IncludeDecodedEvents { get; init; } = true;
    public bool IncludeStationUpdates { get; init; } = true;
    public bool IncludeWeatherUpdates { get; init; } = true;
    public bool IncludeObjectUpdates { get; init; } = true;
    public bool IncludeMessageUpdates { get; init; } = true;
    public bool IncludeAlertUpdates { get; init; } = true;
    public bool IncludeDiagnostics { get; init; } = true;
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTimestamp { get; init; } = DateTimeOffset.UtcNow;

    public bool ReadOnlyStreamingOnly => true;
    public bool HasTransmitCapability => false;

    public static WebSocketEventStreamConfiguration Default { get; } = new();
}
