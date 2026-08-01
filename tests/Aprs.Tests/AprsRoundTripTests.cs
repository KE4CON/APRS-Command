using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Generate ↔ parse round-trip conformance: what our formatters emit must parse back, with our own
/// parser, to the same station identity and values. Guards against the generate and parse sides
/// drifting apart.
/// </summary>
public sealed class AprsRoundTripTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FixedPositionBeacon_RoundTrips()
    {
        var formatter = new AprsBeaconFormatter();
        var profile = LocalStationProfile.CreateDefault(Now) with
        {
            Callsign = "KD8ABC",
            Ssid = 7,
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            StationComment = "Round trip test",
            BeaconPath = "WIDE1-1,WIDE2-1"
        };

        var result = formatter.FormatFixedPositionBeacon(formatter.CreateInputFromProfile(profile));
        Assert.True(result.IsSuccess);

        var parsed = new AprsParser().Parse(result.Packet!, Now);

        var pos = Assert.IsType<PositionAprsPacket>(parsed);
        Assert.True(parsed.IsValid);
        Assert.Equal("KD8ABC", pos.SourceCallsign);
        Assert.Equal(7, pos.SourceSsid);
        Assert.Equal(39.0583, pos.Latitude!.Value, 4);   // uncompressed format carries 0.01' (~60 ft)
        Assert.Equal(-84.5083, pos.Longitude!.Value, 4);
        Assert.Equal('/', pos.SymbolTableIdentifier);
        Assert.Equal('-', pos.SymbolCode);
        Assert.Equal("Round trip test", pos.Comment);
        Assert.Equal("WIDE1-1", pos.Path[0]);
        Assert.Equal("WIDE2-1", pos.Path[1]);
    }

    [Fact]
    public void StatusBeacon_RoundTrips()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatStatusBeacon(
            "N0CALL", AprsConstants.ToCall, new[] { "WIDE1-1" }, "Net control online");
        Assert.True(result.IsSuccess);

        var parsed = new AprsParser().Parse(result.Packet!, Now);

        var status = Assert.IsType<StatusAprsPacket>(parsed);
        Assert.True(parsed.IsValid);
        Assert.Equal("N0CALL", status.SourceCallsign);
        Assert.Equal("Net control online", status.StatusText);
    }

    [Fact]
    public void EmittedBeacon_CarriesTheAllocatedTocall()
    {
        // Round-trip guard that ties back to the device-ID work: our packets self-identify as APCMD0.
        var formatter = new AprsBeaconFormatter();
        var profile = LocalStationProfile.CreateDefault(Now) with
        {
            Callsign = "W1AW",
            FixedLatitude = 41.7,
            FixedLongitude = -72.7
        };

        var result = formatter.FormatFixedPositionBeacon(formatter.CreateInputFromProfile(profile));
        var parsed = new AprsParser().Parse(result.Packet!, Now);

        Assert.Equal(AprsConstants.ToCall, parsed.Destination);
    }

    // ── Objects ──────────────────────────────────────────────────────────────

    private static (AprsObjectEditorService Editor, AprsObjectEditModel Draft) CreateObjectEditor()
    {
        var manager = new AprsObjectManager();
        var profileService = new LocalStationProfileService(Now);
        profileService.UpdateProfile(profileService.GetCurrentProfile() with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, Now);
        var editor = new AprsObjectEditorService(manager, profileService);
        var draft = editor.CreateNewDraft(Now) with
        {
            ObjectName = "CHECKPNT1",
            Latitude = 39.058333,
            Longitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            Comment = "Checkpoint 1"
        };
        return (editor, draft);
    }

    [Fact]
    public void LiveObject_RoundTrips()
    {
        // 14:25:30 UTC deliberately has minutes ≥ 24 — the case the old HHMMSS+'z' emitter mangled into
        // an invalid "hour" field. The DHM-zulu form encodes day 10, hour 14, minute 25.
        var emitAt = new DateTimeOffset(2026, 6, 10, 14, 25, 30, TimeSpan.Zero);
        var (editor, draft) = CreateObjectEditor();

        var save = editor.Save(draft, emitAt);
        Assert.True(save.IsSuccess);

        var obj = Assert.IsType<ObjectAprsPacket>(new AprsParser().Parse(save.PacketPreview!, Now));
        Assert.True(obj.IsValid);
        Assert.Equal("CHECKPNT1", obj.ObjectName);
        Assert.True(obj.IsAlive);
        Assert.False(obj.IsKilled);
        Assert.Equal(39.0583, obj.Latitude!.Value, 4);
        Assert.Equal(-84.5083, obj.Longitude!.Value, 4);
        Assert.Equal('/', obj.SymbolTableIdentifier);
        Assert.Equal('-', obj.SymbolCode);
        Assert.Equal("Checkpoint 1", obj.Comment);

        // Timestamp is conformant DHM-zulu: DDHHMMz with an in-range hour and minute.
        Assert.Equal("101425z", obj.Timestamp);
        Assert.EndsWith("z", obj.Timestamp);
        Assert.InRange(int.Parse(obj.Timestamp!.Substring(2, 2)), 0, 23); // hour
        Assert.InRange(int.Parse(obj.Timestamp!.Substring(4, 2)), 0, 59); // minute
    }

    [Fact]
    public void KilledObject_RoundTrips_WithUnderscoreIndicator()
    {
        var (editor, draft) = CreateObjectEditor();

        var save = editor.MarkKilled(draft, Now);
        Assert.True(save.IsSuccess);

        var obj = Assert.IsType<ObjectAprsPacket>(new AprsParser().Parse(save.PacketPreview!, Now));
        Assert.True(obj.IsKilled);
        Assert.False(obj.IsAlive);
        Assert.Equal("CHECKPNT1", obj.ObjectName);
        // The kill indicator on the wire is '_', not '*'.
        Assert.Contains(";CHECKPNT1_", save.PacketPreview);
    }
}
