using Aprs.Mapping;
using Xunit;

namespace Aprs.Tests;

public sealed class MaidenheadGridLocatorTests
{
    [Theory]
    [InlineData(38.92, -77.07, "FM18")]
    [InlineData(41.7148, -72.7272, "FN31")]
    [InlineData(39.0583, -84.5083, "EM79")]
    public void FromCoordinates_ReturnsExpectedFourCharacterGrid(double latitude, double longitude, string expectedGrid)
    {
        var grid = MaidenheadGridLocator.FromCoordinates(latitude, longitude, precision: 4);

        Assert.Equal(expectedGrid, grid);
    }

    [Fact]
    public void FromCoordinates_ReturnsSixCharacterGrid()
    {
        var grid = MaidenheadGridLocator.FromCoordinates(38.92, -77.07);

        Assert.Equal(6, grid.Length);
        Assert.StartsWith("FM18", grid);
    }

    [Fact]
    public void FromCoordinates_RejectsInvalidCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MaidenheadGridLocator.FromCoordinates(91, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MaidenheadGridLocator.FromCoordinates(0, 181));
    }

    [Fact]
    public void ToCoordinates_ReturnsCenterOfSixCharacterSquare()
    {
        // FN31pr is the well-known ARRL HQ grid; its center is ~41.71N, 72.73W.
        var (latitude, longitude) = MaidenheadGridLocator.ToCoordinates("FN31pr");

        Assert.Equal(41.73, latitude, 1);
        Assert.Equal(-72.71, longitude, 1);
    }

    [Fact]
    public void ToCoordinates_FourCharacterReturnsSquareCenter()
    {
        // FM18 spans 38–39N and 78–76W; its center is 38.5N, 77W.
        var (latitude, longitude) = MaidenheadGridLocator.ToCoordinates("FM18");

        Assert.Equal(38.5, latitude, 3);
        Assert.Equal(-77.0, longitude, 3);
    }

    [Theory]
    [InlineData(38.92, -77.07)]
    [InlineData(41.7148, -72.7272)]
    [InlineData(-33.87, 151.21)]
    public void ToCoordinates_RoundTripsWithinGridResolution(double latitude, double longitude)
    {
        var grid = MaidenheadGridLocator.FromCoordinates(latitude, longitude);
        var (roundLat, roundLon) = MaidenheadGridLocator.ToCoordinates(grid);

        // A 6-character square is ~2.5' lat × 5' lon, so the center is within ~0.05° of the input.
        Assert.True(Math.Abs(roundLat - latitude) < 0.06, $"lat off by {roundLat - latitude}");
        Assert.True(Math.Abs(roundLon - longitude) < 0.09, $"lon off by {roundLon - longitude}");
    }

    [Fact]
    public void ToCoordinates_RejectsTooShortLocator()
    {
        Assert.Throws<ArgumentException>(() => MaidenheadGridLocator.ToCoordinates("FM"));
    }
}
