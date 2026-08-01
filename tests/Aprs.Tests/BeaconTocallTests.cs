using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Guards that every APRS Command transmit path uses the allocated device-ID tocall
/// (<see cref="AprsConstants.ToCall"/> = APCMD0, registered with the APRS Foundation) as the AX.25
/// destination — not a generic "APRS". If the app's own packets do not carry the tocall, the
/// device-ID database cannot identify them as APRS Command, which defeats the allocation.
/// </summary>
public sealed class BeaconTocallTests
{
    [Fact]
    public void DefaultTransmitConfigs_UseAllocatedTocall()
    {
        Assert.Equal("APCMD0", AprsConstants.ToCall);
        Assert.Equal(AprsConstants.ToCall, BeaconSchedulerConfiguration.Default.Destination);
        Assert.Equal(AprsConstants.ToCall, WeatherBeaconConfiguration.Default.AprsDestination);
        Assert.Equal(AprsConstants.ToCall, AprsWeatherFormatterOptions.Default.Destination);
        Assert.Equal(AprsConstants.ToCall, AprsMessageRetryConfiguration.Default.Destination);
    }

    [Fact]
    public async Task PositionBeaconDestination_StaysOnTocall_AfterSettingsSave()
    {
        // Regression: the settings-save path (ApplySettings -> UpdateConfiguration) used to reset the
        // beacon destination to a hardcoded "APRS", so beacons stopped identifying as APRS Command
        // the moment an operator saved their station settings.
        var settings = AppSettings.Default with
        {
            Station = StationProfile.Default with
            {
                Callsign = "W1AW",
                Latitude = 41.7,
                Longitude = -72.7,
                TransmitEnabled = true,
                AprsIsTransmitEnabled = true,
            }
        };

        var service = BeaconService.CreateFromSettings(settings);
        try
        {
            service.ApplySettings(settings);

            var result = await service.BeaconNowAsync();
            var packet = result.Packet ?? service.GetState().LastGeneratedBeaconPacket;

            Assert.NotNull(packet);
            Assert.Contains(">APCMD0", packet);
            Assert.DoesNotContain(">APRS", packet);
        }
        finally
        {
            await service.DisposeAsync();
        }
    }
}
