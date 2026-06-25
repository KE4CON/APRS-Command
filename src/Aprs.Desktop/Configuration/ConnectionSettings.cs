namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted connection configuration: a list of typed ports the operator can run simultaneously
/// (the decided connection model). Each <see cref="ConnectionPort"/> carries its own type, settings,
/// and enable/receive/transmit switches.
///
/// <para>Defaults to a single receive-only APRS-IS port so a new install shows traffic without setup
/// and cannot transmit by accident.</para>
/// </summary>
public sealed record ConnectionSettings(IReadOnlyList<ConnectionPort> Ports)
{
    public static ConnectionSettings Default { get; } = new([ConnectionPort.DefaultAprsIs()]);

    /// <summary>
    /// Returns a settings value guaranteed to have at least one usable port, with every port's
    /// type-specific configuration filled in. An empty or missing list (e.g. a pre-port-list file)
    /// falls back to <see cref="Default"/>.
    /// </summary>
    public ConnectionSettings Normalized()
    {
        if (Ports is null || Ports.Count == 0)
        {
            return Default;
        }

        return new ConnectionSettings(Ports.Select(p => p.Normalized()).ToList());
    }
}
