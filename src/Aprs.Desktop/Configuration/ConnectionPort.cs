namespace Aprs.Desktop.Configuration;

/// <summary>
/// One connection port in the operator's list. A port has an identity, a type, its own
/// enable / receive / transmit switches, and the settings for its type. Multiple ports run at once
/// (e.g. an RF port plus an APRS-IS port), which is what an iGate needs.
/// </summary>
public sealed record ConnectionPort(
    string Id,
    string Name,
    ConnectionPortType Type,
    bool Enabled,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    PortConfiguration Configuration)
{
    /// <summary>
    /// The default first-run port: a single APRS-IS connection, enabled and receive-only, so a new
    /// operator sees stations without configuring anything and cannot transmit by accident.
    /// </summary>
    public static ConnectionPort DefaultAprsIs() => new(
        Id: "aprs-is",
        Name: "APRS-IS",
        Type: ConnectionPortType.AprsIs,
        Enabled: true,
        ReceiveEnabled: true,
        TransmitEnabled: false,
        Configuration: PortConfiguration.ForAprsIs(Aprs.Transport.AprsIsClientConfiguration.Default));

    /// <summary>Returns this port with its type-specific configuration guaranteed present.</summary>
    public ConnectionPort Normalized() => this with { Configuration = (Configuration ?? new PortConfiguration()).EnsureFor(Type) };
}
