using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Status report (DTI '&gt;') decomposition per APRS Protocol Reference §16, cross-checked against Dire
/// Wolf's decoder: an optional leading DHM-zulu timestamp OR Maidenhead locator+symbol, and an optional
/// trailing beam-heading/ERP ('^') extension, with the remainder as the human-readable message.
/// </summary>
public sealed class AprsStatusReportTests
{
    [Fact]
    public void PlainText_IsUnchanged_WithNoExtras()
    {
        var s = AprsStatusReport.Parse("Net control station online");

        Assert.Equal("Net control station online", s.Message);
        Assert.Null(s.Timestamp);
        Assert.Null(s.MaidenheadLocator);
        Assert.Null(s.BeamHeadingDegrees);
        Assert.Null(s.EffectiveRadiatedPowerWatts);
    }

    [Fact]
    public void Empty_ParsesToEmptyMessage()
    {
        var s = AprsStatusReport.Parse("");

        Assert.Equal("", s.Message);
        Assert.Null(s.Timestamp);
    }

    [Fact]
    public void LeadingZuluTimestamp_IsExtracted()
    {
        var s = AprsStatusReport.Parse("092345zNet control active");

        Assert.Equal("092345z", s.Timestamp);
        Assert.Equal("Net control active", s.Message);
        Assert.Null(s.MaidenheadLocator);
    }

    [Fact]
    public void TimestampOnly_LeavesEmptyMessage()
    {
        var s = AprsStatusReport.Parse("092345z");

        Assert.Equal("092345z", s.Timestamp);
        Assert.Equal("", s.Message);
    }

    [Theory]
    [InlineData("12345zText")]   // only 5 digits before 'z'
    [InlineData("1234567Text")]  // 7th char isn't 'z'
    [InlineData("09234azMore")]  // non-digit in the field
    public void NonTimestamp_IsTreatedAsPlainText(string body)
    {
        var s = AprsStatusReport.Parse(body);

        Assert.Null(s.Timestamp);
        Assert.Equal(body, s.Message);
    }

    [Fact]
    public void SixCharMaidenhead_NoComment()
    {
        var s = AprsStatusReport.Parse("IO91SX/G");

        Assert.Equal("IO91SX", s.MaidenheadLocator);
        Assert.Equal('/', s.SymbolTableIdentifier);
        Assert.Equal('G', s.SymbolCode);
        Assert.Equal("", s.Message);
    }

    [Fact]
    public void FourCharMaidenhead_WithComment()
    {
        var s = AprsStatusReport.Parse("IO91/- Home station");

        Assert.Equal("IO91", s.MaidenheadLocator);
        Assert.Equal('/', s.SymbolTableIdentifier);
        Assert.Equal('-', s.SymbolCode);
        Assert.Equal("Home station", s.Message);
    }

    [Fact]
    public void SixCharMaidenhead_LowercaseSubsquare_WithComment()
    {
        var s = AprsStatusReport.Parse("FN20qa/# Digipeater");

        Assert.Equal("FN20qa", s.MaidenheadLocator);
        Assert.Equal('#', s.SymbolCode);
        Assert.Equal("Digipeater", s.Message);
    }

    [Fact]
    public void GridLikeText_WithoutSpaceOrEndAfterSymbol_IsPlainText()
    {
        // "IO91SXABC" has no space/end after a candidate symbol, so it is ordinary text, not a locator.
        var s = AprsStatusReport.Parse("IO91SXABC");

        Assert.Null(s.MaidenheadLocator);
        Assert.Equal("IO91SXABC", s.Message);
    }

    [Theory]
    [InlineData("Big antenna^88", "Big antenna", 80, 640)]   // heading 8*10=80°, ERP 8²*10=640W
    [InlineData("Beam^A9", "Beam", 100, 810)]                // 'A' -> 100°, '9' -> 810W
    [InlineData("North^0:", "North", 0, 1000)]               // '0' -> 0°, ':' -> 10²*10=1000W
    public void TrailingBeamHeadingAndErp_AreExtracted(
        string body, string expectedMessage, int expectedBeam, int expectedErp)
    {
        var s = AprsStatusReport.Parse(body);

        Assert.Equal(expectedMessage, s.Message);
        Assert.Equal(expectedBeam, s.BeamHeadingDegrees);
        Assert.Equal(expectedErp, s.EffectiveRadiatedPowerWatts);
    }

    [Fact]
    public void CaretWithUndecodableChars_StaysLiteralText()
    {
        // 'x' is outside the ERP range ('1'..'K'), so "^9x" is not a beam extension.
        var s = AprsStatusReport.Parse("price ^9x");

        Assert.Null(s.BeamHeadingDegrees);
        Assert.Null(s.EffectiveRadiatedPowerWatts);
        Assert.Equal("price ^9x", s.Message);
    }

    [Fact]
    public void Timestamp_And_BeamHeading_Combine()
    {
        var s = AprsStatusReport.Parse("092345zNet^88");

        Assert.Equal("092345z", s.Timestamp);
        Assert.Equal("Net", s.Message);
        Assert.Equal(80, s.BeamHeadingDegrees);
        Assert.Equal(640, s.EffectiveRadiatedPowerWatts);
    }

    // ── Through the full parser ──────────────────────────────────────────────

    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parser_PopulatesStructuredStatusFields_AndKeepsRawText()
    {
        var p = new AprsParser().Parse("KE4CON-1>APRS:>092345zNet control^88", Now);

        var status = Assert.IsType<StatusAprsPacket>(p);
        Assert.Equal("092345zNet control^88", status.RawStatusText); // raw is preserved verbatim
        Assert.Equal("Net control", status.StatusText);              // cleaned display text
        Assert.Equal("092345z", status.Timestamp);
        Assert.Equal(80, status.BeamHeadingDegrees);
        Assert.Equal(640, status.EffectiveRadiatedPowerWatts);
    }
}
