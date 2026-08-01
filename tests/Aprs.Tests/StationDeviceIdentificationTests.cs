using Aprs.Desktop.ViewModels;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// The station viewmodels resolve the sending device/software from the station's destination tocall
/// (device-ID slice 2 — surfacing).
/// </summary>
public sealed class StationDeviceIdentificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static StationMarkerViewModel MarkerWithTocall(string? tocall)
    {
        var marker = StationMarker.Create(
            "KE4CON-9", "KE4CON-9", 39.0583, -84.5083, '/', '-',
            Now, StationLifecycleState.Active, AprsPacketSource.Simulation,
            CourseDegrees: null, SpeedKnots: null, altitudeFeet: null,
            lastPath: new[] { "WIDE1-1" }, comment: null, lastRawPacket: null, packetCount: 1)
            with { Destination = tocall };
        return new StationMarkerViewModel(marker);
    }

    [Fact]
    public void MarkerViewModel_ResolvesDeviceFromTocall()
    {
        var vm = MarkerWithTocall("APCMD0");

        Assert.NotNull(vm.DeviceIdentity);
        Assert.Equal("APRS Command", vm.DeviceIdentity!.Model);
        Assert.Contains("APRS Command", vm.Device);
    }

    [Fact]
    public void MarkerViewModel_UnknownTocall_ShowsUnknown()
    {
        var vm = MarkerWithTocall("XYZZY9");

        Assert.Null(vm.DeviceIdentity);
        Assert.Equal("Unknown", vm.Device);
    }

    [Fact]
    public void StationRowAndDetails_CarryTheDevice()
    {
        var vm = MarkerWithTocall("APCMD0");

        Assert.Contains("APRS Command", new StationListRowViewModel(vm).Device);
        Assert.Contains("APRS Command", new StationDetailsViewModel(vm, Now).Device);
    }
}
