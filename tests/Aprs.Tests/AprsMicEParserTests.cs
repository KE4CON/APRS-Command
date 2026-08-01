using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// MIC-E decode tests. Vectors are hand-constructed from the APRS 1.2 / Dire Wolf decode rules and
/// verified step by step in the comments, so a spec misunderstanding is caught rather than enshrined.
/// </summary>
public sealed class AprsMicEParserTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static AprsPacket Parse(string raw) => new AprsParser().Parse(raw, TestTime);

    // Speed/course info bytes (offset by 28): sp=2, dc=0, se=90  ->  speed 20 kt, course 90 deg.
    private static string SpeedCourse() => new(new[] { (char)30, (char)28, (char)118 });

    [Fact]
    public void MicE_StandardOffDuty_DecodesPositionCourseSpeedSymbol()
    {
        // Destination "SSRUVT": lat digits 3,3,2,5,6,4 -> 33 deg 25.64' ; all three msg chars are in the
        // P-Y (standard) group -> std bits 111 -> "Off Duty"; byte4 U>=P -> North; byte5 V>=P -> +100
        // offset; byte6 T>=P -> West.
        // Info: DTI '`', longitude bytes '('(40,off)->112 deg, '`'(96)->8', 'n'(110)->.82 ; symbol '>' on '/'.
        var info = new string(new[] { '`', '(', '`', 'n' }) + SpeedCourse() + ">/";
        var raw = "KE4CON-9>SSRUVT:" + info;

        var p = Parse(raw);

        var pos = Assert.IsType<PositionAprsPacket>(p);
        Assert.Equal(33.4273, pos.Latitude!.Value, 3);
        Assert.Equal(-112.147, pos.Longitude!.Value, 3);
        Assert.Equal(90, pos.CourseDegrees);
        Assert.Equal(20, pos.SpeedKnots);
        Assert.Equal('/', pos.SymbolTableIdentifier);
        Assert.Equal('>', pos.SymbolCode);
        // "Off Duty" is the idle default and is intentionally not surfaced in the comment.
        Assert.DoesNotContain("[", pos.Comment);
    }

    [Fact]
    public void MicE_AllLowGroupMessageBits_DecodesAsEmergency()
    {
        // Destination "123456": all six chars are plain digits. The three message chars are in the low
        // group -> std=0, cust=0 -> Emergency (per spec). Byte4 '4'<P -> South; byte6 '6'<P -> East.
        // Latitude 12 deg 34.56' South.
        var info = new string(new[] { '`', 'x', '`', 'n' }) + SpeedCourse() + ">/";
        var raw = "KE4CON-9>123456:" + info;

        var p = Parse(raw);

        var pos = Assert.IsType<PositionAprsPacket>(p);
        Assert.Equal(-12.576, pos.Latitude!.Value, 3);
        Assert.True(pos.Longitude!.Value > 0, "byte 6 is low group, so longitude is East (positive).");
        Assert.Equal("[Emergency]", pos.Comment);
    }

    [Fact]
    public void MicE_ShortInformationField_DoesNotThrow_AndIsNotAPosition()
    {
        // Robustness: a truncated MIC-E info field must not throw and must not masquerade as a position.
        var raw = "KE4CON-9>SSRUVT:`x";

        var exception = Record.Exception(() => Parse(raw));

        Assert.Null(exception);
        Assert.IsNotType<PositionAprsPacket>(Parse(raw));
    }
}
