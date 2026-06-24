using Aprs.Transport;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted connection configuration. Groups the transport configs that the engine already
/// models so they round-trip to disk as one section of <see cref="AppSettings"/>.
///
/// NOTE: this is intentionally a flat "one of each" shape for now. The decision about whether
/// connections become a list of typed ports (so RF + APRS-IS can run at once) is tracked as a
/// separate action item; when it is made, only this record changes — the persistence machinery
/// (<see cref="JsonAppSettingsStore"/>) is model-agnostic and does not need to change with it.
/// </summary>
public sealed record ConnectionSettings(
    TcpKissConfiguration TcpKiss,
    SerialKissConfiguration SerialKiss,
    AgwpeConfiguration Agwpe,
    AprsIsClientConfiguration AprsIs)
{
    /// <summary>All transports disabled by default; the operator opts in per connection.</summary>
    public static ConnectionSettings Default { get; } = new(
        TcpKiss: TcpKissConfiguration.Default,
        SerialKiss: SerialKissConfiguration.Default,
        Agwpe: AgwpeConfiguration.Default,
        AprsIs: AprsIsClientConfiguration.Default);
}
