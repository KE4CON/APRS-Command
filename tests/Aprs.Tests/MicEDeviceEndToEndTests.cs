using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// End-to-end: a real MIC-E raw packet flows through the parser, the station database (which tags it as
/// MIC-E from the data-type indicator), the marker, and the viewmodel — and the sending radio is
/// identified from the device code carried in the comment. This exercises the whole <c>IsMicE</c> spine,
/// not just the matching table.
/// </summary>
public sealed class MicEDeviceEndToEndTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    // Speed/course info bytes (offset by 28): sp=2, dc=0, se=90 -> 20 kt, 90 deg. Same construction as
    // the decode tests; destination "SSRUVT" decodes to a valid North-West position with "Off Duty"
    // message bits, so no status prefix decorates the comment.
    private static string MicEBody(string deviceComment)
        => new string(new[] { '`', '(', '`', 'n', (char)30, (char)28, (char)118 }) + ">/" + deviceComment;

    private static StationMarkerViewModel Ingest(string raw)
    {
        var packet = new AprsParser().Parse(raw, TestTime);
        var db = new StationDatabase();
        db.ProcessPacket(packet, AprsPacketSource.Rf);

        var snapshot = Assert.Single(db.GetAllStations());
        Assert.True(snapshot.IsMicE, "the database should tag a MIC-E packet from its DTI");

        Assert.True(StationMarker.TryCreate(snapshot, out var marker));
        Assert.True(marker!.IsMicE, "the marker should carry the MIC-E flag");
        return new StationMarkerViewModel(marker);
    }

    [Fact]
    public void LegacyKenwood_MicEPacket_IdentifiesTheRadio()
    {
        // Comment "]" is the TM-D700's device indicator.
        var vm = Ingest("KE4CON-9>SSRUVT:" + MicEBody("]"));

        Assert.NotNull(vm.DeviceIdentity);
        Assert.Equal("TM-D700", vm.DeviceIdentity!.Model);
        Assert.Equal("Kenwood", vm.DeviceIdentity.Vendor);
        Assert.Contains("TM-D700", vm.Device);
    }

    [Fact]
    public void ModernYaesu_MicEPacket_IdentifiesFromTrailingCode()
    {
        // A modern radio ends the comment with a two-character code; "_\"" is the Yaesu FTM-350.
        var vm = Ingest("KE4CON-9>SSRUVT:" + MicEBody("hi _\""));

        Assert.NotNull(vm.DeviceIdentity);
        Assert.Equal("FTM-350", vm.DeviceIdentity!.Model);
        Assert.Contains("Yaesu", vm.DeviceIdentity.Vendor);
    }
}
