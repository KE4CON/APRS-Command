using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Deep-audit: ApplySettings is called on every GPS position write-back. It must NOT tear down and rebuild
/// the APRS-IS client when only position (or other non-connection) settings changed — doing so thrashed the
/// socket ~1 Hz for a mobile station and orphaned the client the message coordinator captured. Transmit is
/// left disabled so the built client never opens a socket during the test.
/// </summary>
public sealed class BeaconServiceApplySettingsTests
{
    private static AppSettings SettingsWithPasscode(string passcode, double latitude)
    {
        var isConfig = AprsIsClientConfiguration.Default with
        {
            Passcode = passcode,
            ReconnectEnabled = false,
            ReceiveOnly = true,
            TransmitEnabled = false,
        };
        var port = new ConnectionPort(
            Id: "aprs-is", Name: "APRS-IS", Type: ConnectionPortType.AprsIs,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: PortConfiguration.ForAprsIs(isConfig));

        return AppSettings.Default with
        {
            Station = AppSettings.Default.Station with
            {
                Callsign = "KE4CON",
                Ssid = 9,
                Latitude = latitude,
                Longitude = -84.0,
                TransmitEnabled = false,          // client is built (valid passcode) but never connects
                AprsIsTransmitEnabled = false,
            },
            Connections = new ConnectionSettings([port]),
        };
    }

    [Fact]
    public void ApplySettings_PositionOnlyChange_DoesNotRebuildClient()
    {
        var beacon = BeaconService.CreateFromSettings(SettingsWithPasscode("12345", latitude: 39.0));
        var original = beacon.AprsIsClient;
        Assert.NotNull(original); // a valid passcode builds a (non-connected) client

        beacon.ApplySettings(SettingsWithPasscode("12345", latitude: 40.0)); // only the position changed

        Assert.Same(original, beacon.AprsIsClient); // NOT rebuilt
    }

    [Fact]
    public void ApplySettings_PasscodeChange_RebuildsClient()
    {
        var beacon = BeaconService.CreateFromSettings(SettingsWithPasscode("12345", latitude: 39.0));
        var original = beacon.AprsIsClient;
        Assert.NotNull(original);

        beacon.ApplySettings(SettingsWithPasscode("54321", latitude: 39.0)); // connection-relevant change

        Assert.NotSame(original, beacon.AprsIsClient); // rebuilt
    }
}
