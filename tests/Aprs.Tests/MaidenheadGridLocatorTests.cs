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
}
