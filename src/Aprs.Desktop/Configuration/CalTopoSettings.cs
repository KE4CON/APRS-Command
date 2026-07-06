namespace Aprs.Desktop.Configuration;

/// <summary>
/// Settings for the CalTopo / SARTopo live position forwarding feature.
///
/// When enabled, APRS Command forwards received station positions to a
/// CalTopo map using the public position reporting endpoint:
///   https://caltopo.com/api/v1/position/report/other
///
/// This lets SAR coordinators watching CalTopo see APRS-tracked field
/// operator positions overlaid on their assignment segment maps in real
/// time — without any manual export/import step.
///
/// Setup on the CalTopo side:
///   1. Open your CalTopo map.
///   2. Click + Add → Locator → Live Track – Fleet, Email, Other.
///   3. Configure the locator and note the 4–6 character Map ID from
///      the URL (e.g. caltopo.com/m/XXXX → map ID is XXXX).
///   4. Enter that Map ID in APRS Command settings.
///
/// The map ID is all that is required — no CalTopo account, API key,
/// or authentication is needed for this endpoint.
/// </summary>
public sealed record CalTopoSettings(
    bool   Enabled,
    string MapId,
    bool   ForwardAprsIsPackets,
    bool   ForwardRfPackets,
    int    MinimumIntervalSeconds)
{
    public static CalTopoSettings Default { get; } = new(
        Enabled:                false,
        MapId:                  string.Empty,
        ForwardAprsIsPackets:   true,
        ForwardRfPackets:       true,
        MinimumIntervalSeconds: 60);
}
