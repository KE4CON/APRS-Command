using Aprs.Transport;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Holds the type-specific settings for a <see cref="ConnectionPort"/>. Only the field matching the
/// port's <see cref="ConnectionPortType"/> is authoritative; the others are null.
///
/// <para>This is the "tagged record" approach chosen for the connection model: a discriminator
/// (the port's <see cref="ConnectionPort.Type"/>) plus optional per-type configs, rather than JSON
/// polymorphism. It keeps the corruption-resilient load simple — a malformed or missing config for a
/// given type just falls back to that type's default, with no custom converters.</para>
/// </summary>
public sealed record PortConfiguration(
    AprsIsClientConfiguration? AprsIs = null,
    TcpKissConfiguration? NetworkTncKiss = null,
    AgwpeConfiguration? Agwpe = null,
    SerialKissConfiguration? SerialKiss = null)
{
    public static PortConfiguration ForAprsIs(AprsIsClientConfiguration configuration) => new(AprsIs: configuration);

    public static PortConfiguration ForNetworkTncKiss(TcpKissConfiguration configuration) => new(NetworkTncKiss: configuration);

    public static PortConfiguration ForAgwpe(AgwpeConfiguration configuration) => new(Agwpe: configuration);

    public static PortConfiguration ForSerialKiss(SerialKissConfiguration configuration) => new(SerialKiss: configuration);

    /// <summary>
    /// Returns this configuration with the field matching <paramref name="type"/> guaranteed
    /// non-null (filling in that type's default if it was missing). Used on load so a port always
    /// has usable settings for its type even if the saved file omitted them.
    /// </summary>
    public PortConfiguration EnsureFor(ConnectionPortType type) => type switch
    {
        ConnectionPortType.AprsIs => this with { AprsIs = AprsIs ?? AprsIsClientConfiguration.Default },
        ConnectionPortType.Agwpe => this with { Agwpe = Agwpe ?? AgwpeConfiguration.Default },
        ConnectionPortType.SerialKiss => this with { SerialKiss = SerialKiss ?? SerialKissConfiguration.Default },
        // Managed local modem connects over loopback KISS-TCP, so it shares the Network TNC config
        // until its dedicated audio/PTT settings are added.
        ConnectionPortType.NetworkTncKiss or ConnectionPortType.ManagedLocalModem
            => this with { NetworkTncKiss = NetworkTncKiss ?? TcpKissConfiguration.Default },
        _ => this
    };
}
