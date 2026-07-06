using Aprs.Desktop.Mapping;
using Aprs.Desktop.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class GeoFileImportExportServiceTests
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "APRSCommandTests_Geo");

    public GeoFileImportExportServiceTests()
    {
        Directory.CreateDirectory(TempDir);
    }

    // ── Coordinate conversion ─────────────────────────────────────────────

    [Fact]
    public void LatLonToWorld_PrimeMeridianEquator_IsOrigin()
    {
        var (x, y) = GeoFileImportExportService.LatLonToWorld(0, 0);
        Assert.Equal(0.0, x, 1);
        Assert.Equal(0.0, y, 1);
    }

    [Fact]
    public void LatLonToWorld_RoundTrip_PreservesCoordinates()
    {
        var (lat, lon) = (41.9756, -88.4553); // Chicagoland
        var (x, y) = GeoFileImportExportService.LatLonToWorld(lat, lon);
        var (lat2, lon2) = GeoFileImportExportService.WorldToLatLon(x, y);
        Assert.Equal(lat, lat2, 4);
        Assert.Equal(lon, lon2, 4);
    }

    [Theory]
    [InlineData(49.5, -72.75)]   // Spec §9 example position
    [InlineData(39.058333, -84.508333)] // Ohio
    [InlineData(-33.8688, 151.2093)]    // Sydney (southern hemisphere)
    [InlineData(85.0, 179.0)]           // Near north pole/date line
    public void LatLonToWorld_RoundTrip_AllQuadrants(double lat, double lon)
    {
        var (x, y) = GeoFileImportExportService.LatLonToWorld(lat, lon);
        var (lat2, lon2) = GeoFileImportExportService.WorldToLatLon(x, y);
        Assert.Equal(lat, lat2, 3);
        Assert.Equal(lon, lon2, 3);
    }

    // ── GPX import ────────────────────────────────────────────────────────

    [Fact]
    public void ImportGpx_ValidTrack_ReturnsShape()
    {
        var gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <name>Test Track</name>
                <trkseg>
                  <trkpt lat="41.975" lon="-88.455"/>
                  <trkpt lat="41.980" lon="-88.460"/>
                  <trkpt lat="41.985" lon="-88.465"/>
                </trkseg>
              </trk>
            </gpx>
            """;
        var path = WriteTemp("track.gpx", gpx);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(result.Shapes);
        Assert.Equal(DrawShapeType.Line, result.Shapes[0].ShapeType);
        Assert.Equal("Test Track", result.Shapes[0].Label);
        Assert.Equal(3, result.Shapes[0].Points.Count);
    }

    [Fact]
    public void ImportGpx_Waypoints_ReturnedAsWaypoints()
    {
        var gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Test" xmlns="http://www.topografix.com/GPX/1/1">
              <wpt lat="41.975" lon="-88.455">
                <name>Staging Area A</name>
                <desc>Primary staging area for EmComm deployment</desc>
                <ele>180.5</ele>
              </wpt>
              <wpt lat="41.990" lon="-88.470">
                <name>Net Control</name>
              </wpt>
            </gpx>
            """;
        var path = WriteTemp("waypoints.gpx", gpx);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Equal(2, result.Waypoints.Count);
        Assert.Equal("Staging Area A", result.Waypoints[0].Name);
        Assert.Equal(41.975, result.Waypoints[0].Latitude, 3);
        Assert.Equal(-88.455, result.Waypoints[0].Longitude, 3);
        Assert.Equal(180.5, result.Waypoints[0].AltitudeMetres!.Value, 1);
        Assert.Equal("Net Control", result.Waypoints[1].Name);
        Assert.Null(result.Waypoints[1].AltitudeMetres);
    }

    [Fact]
    public void ImportGpx_Route_ReturnedAsGoldShape()
    {
        var gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Test" xmlns="http://www.topografix.com/GPX/1/1">
              <rte>
                <name>Event Course</name>
                <rtept lat="41.975" lon="-88.455"/>
                <rtept lat="41.980" lon="-88.460"/>
              </rte>
            </gpx>
            """;
        var path = WriteTemp("route.gpx", gpx);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Single(result.Shapes);
        Assert.Equal("Event Course", result.Shapes[0].Label);
        Assert.Equal("#C8961E", result.Shapes[0].Color); // gold for routes
    }

    [Fact]
    public void ImportGpx_MultipleSegments_MergedIntoOneShape()
    {
        var gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <name>Multi-segment</name>
                <trkseg>
                  <trkpt lat="41.975" lon="-88.455"/>
                  <trkpt lat="41.980" lon="-88.460"/>
                </trkseg>
                <trkseg>
                  <trkpt lat="41.985" lon="-88.465"/>
                  <trkpt lat="41.990" lon="-88.470"/>
                </trkseg>
              </trk>
            </gpx>
            """;
        var path = WriteTemp("multiseg.gpx", gpx);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Single(result.Shapes);
        Assert.Equal(4, result.Shapes[0].Points.Count);
    }

    // ── KML import ────────────────────────────────────────────────────────

    [Fact]
    public void ImportKml_LineString_ReturnsShape()
    {
        var kml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Event Route</name>
                  <LineString>
                    <coordinates>
                      -88.455,41.975,0 -88.460,41.980,0 -88.465,41.985,0
                    </coordinates>
                  </LineString>
                </Placemark>
              </Document>
            </kml>
            """;
        var path = WriteTemp("route.kml", kml);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(result.Shapes);
        Assert.Equal("Event Route", result.Shapes[0].Label);
        Assert.Equal(DrawShapeType.Line, result.Shapes[0].ShapeType);
        Assert.Equal(3, result.Shapes[0].Points.Count);
    }

    [Fact]
    public void ImportKml_Point_ReturnedAsWaypoint()
    {
        var kml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Command Post</name>
                  <description>Incident command post location</description>
                  <Point>
                    <coordinates>-88.455,41.975,185.0</coordinates>
                  </Point>
                </Placemark>
              </Document>
            </kml>
            """;
        var path = WriteTemp("point.kml", kml);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Single(result.Waypoints);
        Assert.Equal("Command Post", result.Waypoints[0].Name);
        Assert.Equal(41.975, result.Waypoints[0].Latitude, 3);
        Assert.Equal(-88.455, result.Waypoints[0].Longitude, 3);
        Assert.Equal(185.0, result.Waypoints[0].AltitudeMetres!.Value, 1);
    }

    [Fact]
    public void ImportKml_Polygon_ReturnedAsPolygonShape()
    {
        var kml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Evacuation Zone</name>
                  <Polygon>
                    <outerBoundaryIs>
                      <LinearRing>
                        <coordinates>
                          -88.450,41.970,0 -88.460,41.970,0
                          -88.460,41.980,0 -88.450,41.980,0
                          -88.450,41.970,0
                        </coordinates>
                      </LinearRing>
                    </outerBoundaryIs>
                  </Polygon>
                </Placemark>
              </Document>
            </kml>
            """;
        var path = WriteTemp("polygon.kml", kml);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Single(result.Shapes);
        Assert.Equal(DrawShapeType.Polygon, result.Shapes[0].ShapeType);
        Assert.Equal("Evacuation Zone", result.Shapes[0].Label);
    }

    // ── Error handling ────────────────────────────────────────────────────

    [Fact]
    public void Import_NonExistentFile_ReturnsFailure()
    {
        var result = GeoFileImportExportService.Import("/nonexistent/file.gpx");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Import_KmzFile_ReturnsHelpfulError()
    {
        var result = GeoFileImportExportService.Import("myfile.kmz");
        Assert.False(result.Success);
        Assert.Contains("KMZ", result.ErrorMessage);
        Assert.Contains("zip", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_MalformedXml_ReturnsFailure()
    {
        var path = WriteTemp("bad.gpx", "this is not xml <><>");
        var result = GeoFileImportExportService.Import(path);
        Assert.False(result.Success);
    }

    [Fact]
    public void Import_UnknownRootElement_ReturnsFailure()
    {
        var path = WriteTemp("unknown.xml", "<?xml version=\"1.0\"?><unknown/>");
        var result = GeoFileImportExportService.Import(path);
        Assert.False(result.Success);
        Assert.Contains("Unrecognized file format", result.ErrorMessage);
    }

    // ── GPX export ────────────────────────────────────────────────────────

    [Fact]
    public void ExportToGpx_LineShape_RoundTripsCorrectly()
    {
        var shape = new DrawingShape { ShapeType = DrawShapeType.Line, Label = "Test Track" };
        var (x1, y1) = GeoFileImportExportService.LatLonToWorld(41.975, -88.455);
        var (x2, y2) = GeoFileImportExportService.LatLonToWorld(41.980, -88.460);
        shape.Points.Add((x1, y1));
        shape.Points.Add((x2, y2));

        var gpx = GeoFileImportExportService.ExportToGpx([shape], []);

        Assert.Contains("<gpx", gpx);
        Assert.Contains("<trk>", gpx);
        Assert.Contains("Test Track", gpx);
        Assert.Contains("trkpt", gpx);
        Assert.Contains("41.975", gpx);
        Assert.Contains("-88.455", gpx);
    }

    [Fact]
    public void ExportToGpx_Waypoints_IncludedAsWpt()
    {
        var wpt = new GeoWaypoint("Staging", "Primary staging area", 41.975, -88.455, 180.0);
        var gpx = GeoFileImportExportService.ExportToGpx([], [wpt]);

        Assert.Contains("<wpt", gpx);
        Assert.Contains("Staging", gpx);
        Assert.Contains("180", gpx);
    }

    [Fact]
    public void ExportToGpx_CirclesSkipped()
    {
        var circle = new DrawingShape { ShapeType = DrawShapeType.Circle };
        circle.Points.Add((0, 0));

        var gpx = GeoFileImportExportService.ExportToGpx([circle], []);

        Assert.DoesNotContain("<trk>", gpx);
        Assert.DoesNotContain("<rte>", gpx);
    }

    // ── KML export ────────────────────────────────────────────────────────

    [Fact]
    public void ExportToKml_LineShape_ProducesValidKml()
    {
        var shape = new DrawingShape { ShapeType = DrawShapeType.Line, Label = "Test Line" };
        var (x1, y1) = GeoFileImportExportService.LatLonToWorld(41.975, -88.455);
        var (x2, y2) = GeoFileImportExportService.LatLonToWorld(41.980, -88.460);
        shape.Points.Add((x1, y1));
        shape.Points.Add((x2, y2));

        var kml = GeoFileImportExportService.ExportToKml([shape], []);

        Assert.Contains("<kml", kml);
        Assert.Contains("<LineString>", kml);
        Assert.Contains("Test Line", kml);
        Assert.Contains("41.975", kml);
    }

    [Fact]
    public void ExportToKml_PolygonShape_ProducesPolygon()
    {
        var shape = new DrawingShape { ShapeType = DrawShapeType.Polygon, Label = "Zone A" };
        shape.Points.Add(GeoFileImportExportService.LatLonToWorld(41.970, -88.450));
        shape.Points.Add(GeoFileImportExportService.LatLonToWorld(41.970, -88.460));
        shape.Points.Add(GeoFileImportExportService.LatLonToWorld(41.980, -88.460));

        var kml = GeoFileImportExportService.ExportToKml([shape], []);

        Assert.Contains("<Polygon>", kml);
        Assert.Contains("outerBoundaryIs", kml);
    }

    // ── GPX/KML round-trip ────────────────────────────────────────────────

    [Fact]
    public void GpxRoundTrip_TrackPreservesCoordinates()
    {
        var shape = new DrawingShape { ShapeType = DrawShapeType.Line, Label = "Round Trip" };
        var pts = new[] { (41.975, -88.455), (41.980, -88.460), (41.985, -88.465) };
        foreach (var (lat, lon) in pts)
            shape.Points.Add(GeoFileImportExportService.LatLonToWorld(lat, lon));

        var gpx = GeoFileImportExportService.ExportToGpx([shape], []);
        var path = WriteTemp("roundtrip.gpx", gpx);
        var result = GeoFileImportExportService.Import(path);

        Assert.True(result.Success);
        Assert.Single(result.Shapes);
        Assert.Equal(3, result.Shapes[0].Points.Count);

        // Verify coordinates round-tripped within 10m precision
        for (var i = 0; i < pts.Length; i++)
        {
            var (lat2, lon2) = GeoFileImportExportService.WorldToLatLon(
                result.Shapes[0].Points[i].X,
                result.Shapes[0].Points[i].Y);
            Assert.Equal(pts[i].Item1, lat2, 3);
            Assert.Equal(pts[i].Item2, lon2, 3);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string WriteTemp(string name, string content)
    {
        var path = Path.Combine(TempDir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
