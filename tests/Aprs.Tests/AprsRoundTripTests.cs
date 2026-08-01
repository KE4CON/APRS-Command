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
}
