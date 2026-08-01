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
        => Marker(tocall, comment: null, isMicE: false);

    private static StationMarkerViewModel Marker(string? tocall, string? comment, bool isMicE)
    {
        var marker = StationMarker.Create(
            "KE4CON-9", "KE4CON-9", 39.0583, -84.5083, '/', '-',
            Now, StationLifecycleState.Active, AprsPacketSource.Simulation,
            CourseDegrees: null, SpeedKnots: null, altitudeFeet: null,
            lastPath: new[] { "WIDE1-1" }, comment: comment, lastRawPacket: null, packetCount: 1)
            with { Destination = tocall, IsMicE = isMicE };
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
    public void MarkerViewModel_ResolvesMicERadioFromComment()
    {
        // MIC-E puts the position in the destination field, so the model marker rides in the comment.
        // A TM-D710's comment is "]=" (prefix ']' + suffix '='); the position destination isn't a tocall.
        var vm = Marker(tocall: "T7SYPU", comment: "]=", isMicE: true);

        Assert.NotNull(vm.DeviceIdentity);
        Assert.Equal("TM-D710", vm.DeviceIdentity!.Model);
        Assert.Contains("TM-D710", vm.Device);
    }

    [Fact]
    public void MarkerViewModel_NonMicEComment_IsNotMistakenForARadio()
    {
        // A regular (non-MIC-E) station with an unknown tocall whose comment happens to start with ']'
        // must not be mislabelled a Kenwood — the comment is only consulted for MIC-E packets.
        var vm = Marker(tocall: "XYZZY9", comment: "] see my website", isMicE: false);

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
