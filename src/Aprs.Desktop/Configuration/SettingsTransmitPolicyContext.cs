using Aprs.Services;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Supplies the transmit authority's identity facts from persisted settings:
/// a valid callsign means the station profile is configured (not N0CALL); a valid APRS-IS passcode
/// means a real numeric passcode is set (not the "-1" receive-only sentinel).
///
/// <para>Reads the current settings on each query so changes made in the UI take effect immediately,
/// without needing to restart or re-resolve the authority.</para>
/// </summary>
public sealed class SettingsTransmitPolicyContext : ITransmitPolicyContext
{
    private readonly IAppSettingsStore store;

    public SettingsTransmitPolicyContext(IAppSettingsStore store)
        => this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool HasValidStationCallsign => store.Load().Station.IsConfigured;

    public bool HasValidAprsIsPasscode
    {
        get
        {
            // Valid if any configured APRS-IS port carries a real numeric passcode (not the -1
            // receive-only sentinel).
            foreach (var port in store.Load().Connections.Ports)
            {
                if (port.Type != ConnectionPortType.AprsIs)
                {
                    continue;
                }

                var passcode = port.Configuration.AprsIs?.Passcode?.Trim();
                if (!string.IsNullOrEmpty(passcode)
                    && !string.Equals(passcode, "-1", StringComparison.Ordinal)
                    && int.TryParse(passcode, out var value)
                    && value >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
