using Aprs.Services;

namespace AprsCommand.Api;

public sealed record WebSocketEventStreamClientFilter
{
    public IReadOnlySet<AprsEventCategory>? EventCategories { get; init; }
    public IReadOnlySet<AprsEventType>? EventTypes { get; init; }
    public string? CallsignOrSource { get; init; }
    public bool IncludeRawPackets { get; init; } = true;
    public AprsEventSeverity MinimumSeverity { get; init; } = AprsEventSeverity.Trace;

    public static WebSocketEventStreamClientFilter Default { get; } = new();
}
