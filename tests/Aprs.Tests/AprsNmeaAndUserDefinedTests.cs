using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Covers the two previously-missing APRS data types: raw NMEA GPS (<c>$</c>) decoded into a
/// position, and user-defined (<c>{</c>) recognized as its own type instead of Unknown.
/// </summary>
public sealed class AprsNmeaAndUserDefinedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nmea_GxRMC_DecodesToPositionWithCourseAndSpeed()
    {
        var packet = new AprsParser().Parse(
            "N0CALL>GPS:$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A", Now);

        var position = Assert.IsType<PositionAprsPacket>(packet);
        Assert.Equal('$', position.PositionType);
        Assert.NotNull(position.Latitude);
        Assert.NotNull(position.Longitude);
        Assert.Equal(48.1173, position.Latitude!.Value, 3);
        Assert.Equal(11.5167, position.Longitude!.Value, 3);
        Assert.Equal(22, position.SpeedKnots);
        Assert.Equal(84, position.CourseDegrees);
        Assert.True(position.IsValid);
    }

    [Fact]
    public void Nmea_GxGGA_DecodesToPositionWithAltitude()
    {
        var packet = new AprsParser().Parse(
            "N0CALL>GPS:$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47", Now);

        var position = Assert.IsType<PositionAprsPacket>(packet);
        Assert.Equal('$', position.PositionType);
        Assert.Equal(48.1173, position.Latitude!.Value, 3);
        Assert.Equal(11.5167, position.Longitude!.Value, 3);
        Assert.Equal(1789, position.AltitudeFeet); // 545.4 m -> ~1789 ft
    }

    [Fact]
    public void Nmea_SouthernWesternHemisphere_IsNegated()
    {
        var packet = new AprsParser().Parse(
            "N0CALL>GPS:$GPRMC,010101,A,3350.000,S,15112.000,W,000.0,000.0,010120,,*00", Now);

        var position = Assert.IsType<PositionAprsPacket>(packet);
        Assert.True(position.Latitude!.Value < 0, "southern latitude should be negative");
        Assert.True(position.Longitude!.Value < 0, "western longitude should be negative");
        Assert.Equal(-33.8333, position.Latitude!.Value, 3);
        Assert.Equal(-151.2, position.Longitude!.Value, 3);
    }

    [Fact]
    public void Nmea_NonPositionSentence_FallsThroughToUnknown()
    {
        // GSV (satellites in view) carries no position, so it is left as Unknown rather than
        // pretending to be a position.
        var packet = new AprsParser().Parse("N0CALL>GPS:$GPGSV,3,1,11,01,40,083,46*7B", Now);

        Assert.IsType<UnknownAprsPacket>(packet);
    }

    [Fact]
    public void Nmea_MalformedPositionSentence_IsPositionTaggedButFlagged()
    {
        // A recognized position sentence (RMC) with unparseable coordinates is still a '$' position
        // type — not Unknown — but flagged invalid with no coordinates.
        var packet = new AprsParser().Parse("N0CALL>GPS:$GPRMC,123519,A,BADLAT,N,BADLON,E,,,230394,,*00", Now);

        var position = Assert.IsType<PositionAprsPacket>(packet);
        Assert.Equal('$', position.PositionType);
        Assert.Null(position.Latitude);
        Assert.False(position.IsValid);
    }

    [Fact]
    public void UserDefined_IsRecognizedWithIdAndContent()
    {
        var packet = new AprsParser().Parse("N0CALL>APRS:{Iexperimental payload", Now);

        var userDefined = Assert.IsType<UserDefinedAprsPacket>(packet);
        Assert.Equal('I', userDefined.UserId);
        Assert.Equal("experimental payload", userDefined.Content);
    }
}
