using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aprs.Desktop.Mapping;

namespace Aprs.Desktop.Services;

/// <summary>
/// Result of a GPX or KML import operation.
/// </summary>
public sealed record GeoImportResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<DrawingShape> Shapes,
    IReadOnlyList<GeoWaypoint> Waypoints,
    string? SourceFileName)
{
    public static GeoImportResult Failed(string error) =>
        new(false, error, [], [], null);
}

/// <summary>A named point of interest imported from GPX or KML.</summary>
public sealed record GeoWaypoint(
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    double? AltitudeMetres);

/// <summary>
/// Imports and exports GPX and KML files for use as map overlays.
///
/// GPX support: waypoints (wpt), tracks (trk/trkseg/trkpt), routes (rte/rtept).
/// KML support: Placemark Points, LineStrings, Polygons, MultiGeometry.
/// KMZ: not supported (zip-compressed KML — tell user to extract first).
///
/// Coordinate system: GPX/KML use WGS84 (lat/lon). The shapes are stored
/// in Mapsui world coordinates (EPSG:3857 Web Mercator) to match the drawing
/// layer that renders them.
/// </summary>
public static class GeoFileImportExportService
{
    // ── Import ────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports a GPX or KML file. The format is detected from the file extension
    /// and confirmed from the root XML element.
    /// </summary>
    public static GeoImportResult Import(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".kmz")
            return GeoImportResult.Failed(
                "KMZ files (zipped KML) are not directly supported. " +
                "Rename the file to .zip, extract it, and open the .kml file inside.");

        if (!File.Exists(filePath))
            return GeoImportResult.Failed($"File not found: {filePath}");

        try
        {
            var xml = XDocument.Load(filePath);
            var root = xml.Root;
            if (root is null)
                return GeoImportResult.Failed("File contains no XML content.");

            // Detect format from root element name regardless of extension
            var rootLocal = root.Name.LocalName.ToLowerInvariant();
            return rootLocal switch
            {
                "gpx" => ImportGpx(xml, Path.GetFileName(filePath)),
                "kml" => ImportKml(xml, Path.GetFileName(filePath)),
                _ => GeoImportResult.Failed(
                    $"Unrecognized file format (root element: <{root.Name.LocalName}>). " +
                    "Expected a GPX or KML file.")
            };
        }
        catch (XmlException ex)
        {
            return GeoImportResult.Failed($"XML parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return GeoImportResult.Failed($"Import failed: {ex.Message}");
        }
    }

    // ── GPX ──────────────────────────────────────────────────────────────

    private static GeoImportResult ImportGpx(XDocument xml, string fileName)
    {
        var ns = xml.Root!.Name.Namespace;
        var shapes    = new List<DrawingShape>();
        var waypoints = new List<GeoWaypoint>();

        // Waypoints: <wpt lat="..." lon="..."><name>...</name></wpt>
        foreach (var wpt in xml.Root.Descendants(ns + "wpt"))
        {
            var lat = ParseAttrDouble(wpt, "lat");
            var lon = ParseAttrDouble(wpt, "lon");
            if (lat is null || lon is null) continue;

            var name = wpt.Element(ns + "name")?.Value.Trim() ?? string.Empty;
            var desc = wpt.Element(ns + "desc")?.Value.Trim() ?? string.Empty;
            var ele  = ParseElemDouble(wpt, ns + "ele");

            waypoints.Add(new GeoWaypoint(name, desc, lat.Value, lon.Value, ele));
        }

        // Tracks: <trk><trkseg><trkpt lat lon>
        foreach (var trk in xml.Root.Descendants(ns + "trk"))
        {
            var name  = trk.Element(ns + "name")?.Value.Trim() ?? "GPX Track";
            var color = "#1A56A0"; // APRS blue for tracks
            var allPts = new List<(double X, double Y)>();

            foreach (var seg in trk.Elements(ns + "trkseg"))
            {
                foreach (var pt in seg.Elements(ns + "trkpt"))
                {
                    var lat = ParseAttrDouble(pt, "lat");
                    var lon = ParseAttrDouble(pt, "lon");
                    if (lat is null || lon is null) continue;
                    allPts.Add(LatLonToWorld(lat.Value, lon.Value));
                }
            }

            if (allPts.Count >= 2)
            {
                var shape = new DrawingShape
                {
                    ShapeType   = DrawShapeType.Line,
                    Label       = name,
                    Color       = color,
                    StrokeWidth = 2.5,
                };
                shape.Points.AddRange(allPts);
                shapes.Add(shape);
            }
        }

        // Routes: <rte><rtept lat lon>
        foreach (var rte in xml.Root.Descendants(ns + "rte"))
        {
            var name  = rte.Element(ns + "name")?.Value.Trim() ?? "GPX Route";
            var pts   = new List<(double X, double Y)>();

            foreach (var pt in rte.Elements(ns + "rtept"))
            {
                var lat = ParseAttrDouble(pt, "lat");
                var lon = ParseAttrDouble(pt, "lon");
                if (lat is null || lon is null) continue;
                pts.Add(LatLonToWorld(lat.Value, lon.Value));
            }

            if (pts.Count >= 2)
            {
                var shape = new DrawingShape
                {
                    ShapeType   = DrawShapeType.Line,
                    Label       = name,
                    Color       = "#C8961E", // APRS gold for routes
                    StrokeWidth = 2.5,
                };
                shape.Points.AddRange(pts);
                shapes.Add(shape);
            }
        }

        return new GeoImportResult(true, null, shapes, waypoints, fileName);
    }

    // ── KML ──────────────────────────────────────────────────────────────

    private static GeoImportResult ImportKml(XDocument xml, string fileName)
    {
        // KML namespace — may be 2.2 or 2.3
        var ns = xml.Root!.Name.Namespace;
        var shapes    = new List<DrawingShape>();
        var waypoints = new List<GeoWaypoint>();

        foreach (var placemark in xml.Descendants(ns + "Placemark"))
        {
            var name = placemark.Element(ns + "name")?.Value.Trim() ?? string.Empty;
            var desc = placemark.Element(ns + "description")?.Value.Trim() ?? string.Empty;

            // Style color (simplified — just use default colors)
            // Full KML style parsing is complex; use APRS colors as defaults

            // Point
            var pointElem = placemark.Descendants(ns + "Point").FirstOrDefault();
            if (pointElem is not null)
            {
                var coord = ParseKmlCoordinate(pointElem.Element(ns + "coordinates")?.Value);
                if (coord.HasValue)
                    waypoints.Add(new GeoWaypoint(name, desc,
                        coord.Value.Lat, coord.Value.Lon, coord.Value.Alt));
            }

            // LineString
            foreach (var ls in placemark.Descendants(ns + "LineString"))
            {
                var pts = ParseKmlCoordinates(ls.Element(ns + "coordinates")?.Value);
                if (pts.Count >= 2)
                {
                    var shape = new DrawingShape
                    {
                        ShapeType   = DrawShapeType.Line,
                        Label       = name,
                        Color       = "#1A56A0",
                        StrokeWidth = 2.5,
                    };
                    shape.Points.AddRange(pts.Select(p => LatLonToWorld(p.Lat, p.Lon)));
                    shapes.Add(shape);
                }
            }

            // Polygon (outer ring only)
            foreach (var poly in placemark.Descendants(ns + "Polygon"))
            {
                var outerRing = poly.Descendants(ns + "outerBoundaryIs")
                                    .FirstOrDefault()
                                    ?.Descendants(ns + "coordinates")
                                    .FirstOrDefault();
                if (outerRing is null) continue;

                var pts = ParseKmlCoordinates(outerRing.Value);
                if (pts.Count >= 3)
                {
                    var shape = new DrawingShape
                    {
                        ShapeType   = DrawShapeType.Polygon,
                        Label       = name,
                        Color       = "#C8961E",
                        StrokeWidth = 2.0,
                    };
                    shape.Points.AddRange(pts.Select(p => LatLonToWorld(p.Lat, p.Lon)));
                    shapes.Add(shape);
                }
            }
        }

        return new GeoImportResult(true, null, shapes, waypoints, fileName);
    }

    // ── Export ────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports the current map drawings and waypoints as a GPX file.
    /// Tracks are exported as GPX tracks; polygons as routes.
    /// </summary>
    public static string ExportToGpx(
        IEnumerable<DrawingShape> shapes,
        IEnumerable<GeoWaypoint> waypoints,
        string creatorName = "APRS Command")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<gpx version=\"1.1\"");
        sb.AppendLine($"     creator=\"{EscapeXml(creatorName)}\"");
        sb.AppendLine("     xmlns=\"http://www.topografix.com/GPX/1/1\"");
        sb.AppendLine($"     xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        sb.AppendLine($"  <metadata><time>{DateTimeOffset.UtcNow:o}</time></metadata>");

        // Waypoints
        foreach (var wpt in waypoints)
        {
            sb.AppendLine($"  <wpt lat=\"{Lat(wpt.Latitude)}\" lon=\"{Lon(wpt.Longitude)}\">");
            if (!string.IsNullOrWhiteSpace(wpt.Name))
                sb.AppendLine($"    <name>{EscapeXml(wpt.Name)}</name>");
            if (!string.IsNullOrWhiteSpace(wpt.Description))
                sb.AppendLine($"    <desc>{EscapeXml(wpt.Description)}</desc>");
            if (wpt.AltitudeMetres.HasValue)
                sb.AppendLine($"    <ele>{wpt.AltitudeMetres.Value:F1}</ele>");
            sb.AppendLine("  </wpt>");
        }

        // Shapes
        foreach (var shape in shapes)
        {
            if (shape.ShapeType == DrawShapeType.Circle) continue; // no GPX equivalent

            if (shape.ShapeType == DrawShapeType.Line)
            {
                sb.AppendLine("  <trk>");
                if (!string.IsNullOrWhiteSpace(shape.Label))
                    sb.AppendLine($"    <name>{EscapeXml(shape.Label)}</name>");
                sb.AppendLine("    <trkseg>");
                foreach (var (x, y) in shape.Points)
                {
                    var (lat, lon) = WorldToLatLon(x, y);
                    sb.AppendLine($"      <trkpt lat=\"{Lat(lat)}\" lon=\"{Lon(lon)}\"/>");
                }
                sb.AppendLine("    </trkseg>");
                sb.AppendLine("  </trk>");
            }
            else if (shape.ShapeType == DrawShapeType.Polygon)
            {
                sb.AppendLine("  <rte>");
                if (!string.IsNullOrWhiteSpace(shape.Label))
                    sb.AppendLine($"    <name>{EscapeXml(shape.Label)}</name>");
                foreach (var (x, y) in shape.Points)
                {
                    var (lat, lon) = WorldToLatLon(x, y);
                    sb.AppendLine($"  <rtept lat=\"{Lat(lat)}\" lon=\"{Lon(lon)}\"/>");
                }
                sb.AppendLine("  </rte>");
            }
        }

        sb.Append("</gpx>");
        return sb.ToString();
    }

    /// <summary>
    /// Exports the current map drawings and waypoints as a KML file.
    /// </summary>
    public static string ExportToKml(
        IEnumerable<DrawingShape> shapes,
        IEnumerable<GeoWaypoint> waypoints,
        string documentName = "APRS Command Export")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine($"  <Document><name>{EscapeXml(documentName)}</name>");

        // Waypoints as Placemarks
        foreach (var wpt in waypoints)
        {
            sb.AppendLine("  <Placemark>");
            sb.AppendLine($"    <name>{EscapeXml(wpt.Name)}</name>");
            if (!string.IsNullOrWhiteSpace(wpt.Description))
                sb.AppendLine($"    <description>{EscapeXml(wpt.Description)}</description>");
            sb.AppendLine("    <Point><coordinates>");
            sb.AppendLine($"      {Lon(wpt.Longitude)},{Lat(wpt.Latitude)},{wpt.AltitudeMetres ?? 0:F1}");
            sb.AppendLine("    </coordinates></Point>");
            sb.AppendLine("  </Placemark>");
        }

        // Shapes
        foreach (var shape in shapes)
        {
            if (shape.ShapeType == DrawShapeType.Circle) continue;

            sb.AppendLine("  <Placemark>");
            if (!string.IsNullOrWhiteSpace(shape.Label))
                sb.AppendLine($"    <name>{EscapeXml(shape.Label)}</name>");

            if (shape.ShapeType == DrawShapeType.Line)
            {
                sb.AppendLine("    <LineString><coordinates>");
                foreach (var (x, y) in shape.Points)
                {
                    var (lat, lon) = WorldToLatLon(x, y);
                    sb.Append($"      {Lon(lon)},{Lat(lat)},0 ");
                }
                sb.AppendLine();
                sb.AppendLine("    </coordinates></LineString>");
            }
            else if (shape.ShapeType == DrawShapeType.Polygon)
            {
                sb.AppendLine("    <Polygon><outerBoundaryIs><LinearRing><coordinates>");
                foreach (var (x, y) in shape.Points)
                {
                    var (lat, lon) = WorldToLatLon(x, y);
                    sb.Append($"      {Lon(lon)},{Lat(lat)},0 ");
                }
                sb.AppendLine();
                sb.AppendLine("    </coordinates></LinearRing></outerBoundaryIs></Polygon>");
            }

            sb.AppendLine("  </Placemark>");
        }

        sb.AppendLine("  </Document></kml>");
        return sb.ToString();
    }

    // ── Coordinate conversion ─────────────────────────────────────────────

    /// <summary>
    /// Converts WGS84 lat/lon (degrees) to Web Mercator world coordinates (metres).
    /// Uses the standard EPSG:3857 formula — matches Mapsui's coordinate system.
    /// </summary>
    public static (double X, double Y) LatLonToWorld(double lat, double lon)
    {
        const double earthRadius = 6378137.0;
        var x = lon * Math.PI / 180.0 * earthRadius;
        var latRad = lat * Math.PI / 180.0;
        var y = Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0)) * earthRadius;
        return (x, y);
    }

    /// <summary>
    /// Converts Web Mercator world coordinates (metres) back to WGS84 lat/lon.
    /// </summary>
    public static (double Lat, double Lon) WorldToLatLon(double x, double y)
    {
        const double earthRadius = 6378137.0;
        var lon = x / earthRadius * 180.0 / Math.PI;
        var lat = (2.0 * Math.Atan(Math.Exp(y / earthRadius)) - Math.PI / 2.0) * 180.0 / Math.PI;
        return (lat, lon);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static double? ParseAttrDouble(XElement elem, string attr) =>
        double.TryParse(elem.Attribute(attr)?.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? ParseElemDouble(XElement parent, XName name) =>
        double.TryParse(parent.Element(name)?.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static (double Lat, double Lon, double? Alt)? ParseKmlCoordinate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Trim().Split(',');
        if (parts.Length < 2) return null;
        if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return null;
        if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return null;
        double? alt = parts.Length >= 3 && double.TryParse(parts[2].Trim(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : null;
        return (lat, lon, alt);
    }

    private static List<(double Lat, double Lon, double? Alt)> ParseKmlCoordinates(string? raw)
    {
        var result = new List<(double, double, double?)>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var token in raw.Trim().Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            var c = ParseKmlCoordinate(token);
            if (c.HasValue) result.Add(c.Value);
        }
        return result;
    }

    private static string Lat(double v)  => v.ToString("F6", CultureInfo.InvariantCulture);
    private static string Lon(double v)  => v.ToString("F6", CultureInfo.InvariantCulture);
    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("\"", "&quot;").Replace("'", "&apos;");
}
