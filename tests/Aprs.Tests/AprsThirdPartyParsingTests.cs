using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Third-party traffic (DTI '}') carries a complete packet as originally heard, wrapped by a gateway.
/// The parser must unwrap it so the originating station surfaces, and must not recurse without bound.
/// </summary>
public sealed class AprsThirdPartyParsingTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static AprsPacket Parse(string raw) => new AprsParser().Parse(raw, TestTime);

    [Fact]
    public void ThirdParty_UnwrapsToOriginatingStation()
    {
        // A gateway relays KE4CON-9's position onto APRS-IS. We should decode the inner station, not
        // the gateway, so KE4CON-9 appears on the map.
        var raw = "N0GATE>APRS,TCPIP*:}KE4CON-9>APRS,WIDE1-1:!3903.50N/08430.50W>Test";

        var p = Parse(raw);

        var pos = Assert.IsType<PositionAprsPacket>(p);
        Assert.Equal("KE4CON", pos.SourceCallsign);
        Assert.Equal(9, pos.SourceSsid);
        Assert.Equal(39.0583, pos.Latitude!.Value, 3);
        Assert.Equal(-84.5083, pos.Longitude!.Value, 3);
        Assert.Equal('>', pos.SymbolCode);
    }

    [Fact]
    public void ThirdParty_DeeplyNested_DoesNotThrowOrRecurseUnbounded()
    {
        // Pathological chain of nested third-party headers must terminate cleanly.
        var raw = "N0GATE>APRS:" + new string('}', 50) + "junk";

        var exception = Record.Exception(() => Parse(raw));

        Assert.Null(exception);
    }
}
