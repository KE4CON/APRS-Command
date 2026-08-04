using Aprs.Desktop.Mapping;
using Mapsui.Projections;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Correctness tests for drawn-shape measurements. These numbers are shown to operators and must be
/// right — including the Web-Mercator latitude correction (distance × cos(lat), area × cos²(lat)).
/// </summary>
public class ShapeMeasurementsTests
{
    private static (double x, double y) Merc(double lon, double lat) => SphericalMercator.FromLonLat(lon, lat);

    [Fact]
    public void Length_AtEquator_EqualsMercatorLength()
    {
        var a = Merc(0.0, 0.0);
        var b = (a.x + 1000.0, a.y);                  // 1000 Mercator metres east, still on the equator
        var len = ShapeMeasurements.GroundLengthMetres(new[] { (a.x, a.y), b });
        Assert.Equal(1000.0, len, 3);                 // cos(0) = 1
    }

    [Fact]
    public void Length_AwayFromEquator_IsShrunkByCosLatitude()
    {
        // Same 1000 Mercator-metre span at latitude 60° → true ground ≈ 1000 × cos(60°) = 500 m.
        var s = Merc(0.0, 60.0);
        var len = ShapeMeasurements.GroundLengthMetres(new[] { (s.x, s.y), (s.x + 1000.0, s.y) });
        Assert.Equal(500.0, len, 0);                  // cos(60°) = 0.5
    }

    [Fact]
    public void Area_AtEquator_IsShoelaceArea()
    {
        var o = Merc(0.0, 0.0);
        var pts = new[]
        {
            (o.x, o.y), (o.x + 1000, o.y), (o.x + 1000, o.y + 1000), (o.x, o.y + 1000),
        };
        var (area, _, _) = ShapeMeasurements.GroundAreaAndCentroid(pts);
        Assert.Equal(1_000_000.0, area, 0);           // 1000 × 1000 m², cos²(≈0) ≈ 1
    }

    [Theory]
    [InlineData(100, true, "328 ft")]
    [InlineData(1609.344, true, "1.00 mi")]
    [InlineData(500, false, "500 m")]
    [InlineData(1500, false, "1.50 km")]
    public void FormatLength_PicksSensibleUnits(double metres, bool imperial, string expected)
    {
        Assert.Equal(expected, ShapeMeasurements.FormatLength(metres, imperial));
    }

    [Theory]
    [InlineData(4046.8564224, true, "1.00 acres")]
    [InlineData(2_589_988.110336, true, "1.00 sq mi")]
    [InlineData(20000, false, "2.00 ha")]
    [InlineData(2_000_000, false, "2.00 km²")]
    public void FormatArea_PicksSensibleUnits(double squareMetres, bool imperial, string expected)
    {
        Assert.Equal(expected, ShapeMeasurements.FormatArea(squareMetres, imperial));
    }
}
