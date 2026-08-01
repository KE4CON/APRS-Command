using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// !DAO! datum/precision extension (APRS 1.2, aprs.org/aprs12/datum.txt). A trailing !Dxx! token
/// refines the reported position and is stripped from the comment.
/// </summary>
public sealed class AprsDaoExtensionTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static PositionAprsPacket ParsePosition(string raw)
    {
        var p = new AprsParser().Parse(raw, TestTime);
        return Assert.IsType<PositionAprsPacket>(p);
    }

    [Fact]
    public void HumanReadableDao_RefinesPositionAndStripsToken()
    {
        // Base 39 03.50'N / 084 30.50'W. !W42! (uppercase datum) adds .004' to lat, .002' to lon.
        var pos = ParsePosition("N0CALL>APRS:!3903.50N/08430.50W>Test!W42!");

        Assert.Equal(39.0584, pos.Latitude!.Value, 4);   // 39 + 3.504/60
        Assert.Equal(-84.5084, pos.Longitude!.Value, 4); // -(84 + 30.502/60)
        Assert.Equal("Test", pos.Comment);               // DAO token stripped
    }

    [Fact]
    public void Base91Dao_RefinesPositionAndStripsToken()
    {
        // !wAA! (lowercase datum): 'A' -> value 32 -> 32*1.1/10000 = 0.00352' added to each coordinate.
        var pos = ParsePosition("N0CALL>APRS:!3903.50N/08430.50W>Test!wAA!");

        Assert.Equal(39.05839, pos.Latitude!.Value, 5);  // 39 + 3.50352/60
        Assert.Equal("Test", pos.Comment);
    }

    [Fact]
    public void NoDao_LeavesPositionAndCommentUnchanged()
    {
        var pos = ParsePosition("N0CALL>APRS:!3903.50N/08430.50W>Hello world");

        Assert.Equal(39.05833, pos.Latitude!.Value, 5);  // unrefined base position
        Assert.Equal("Hello world", pos.Comment);
    }

    [Fact]
    public void IncidentalBangToken_MidComment_IsNotTreatedAsDao()
    {
        // "Hi! ok!" ends with "! ok!" (space datum but non-space chars) — must NOT be treated as DAO.
        var pos = ParsePosition("N0CALL>APRS:!3903.50N/08430.50W>Hi! ok!");

        Assert.Equal(39.05833, pos.Latitude!.Value, 5);
        Assert.Equal("Hi! ok!", pos.Comment);
    }
}
