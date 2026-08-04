namespace Aprs.Desktop.Mapping;

public enum DrawShapeType { Line, Polygon, Circle, Text }

/// <summary>Fill for polygons and circles. Mapped to the renderer's fill styles in the map view.</summary>
public enum DrawFillStyle { Solid, None, DiagonalHatch, CrossHatch, Horizontal, Vertical, Dotted }

/// <summary>
/// A user-drawn shape on the APRS map — line, polygon, or circle.
/// Stored in world coordinates (Web Mercator EPSG:3857).
/// </summary>
public sealed class DrawingShape
{
    public Guid Id { get; } = Guid.NewGuid();
    public DrawShapeType ShapeType { get; init; }
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF0000"; // vivid red default
    public double StrokeWidth { get; set; } = 3.0;

    /// <summary>Fill pattern — polygons only.</summary>
    public DrawFillStyle FillStyle { get; set; } = DrawFillStyle.Solid;

    /// <summary>Fixed on-screen font size — text shapes with no ground height.</summary>
    public double FontSize { get; set; } = 14.0;

    /// <summary>
    /// Text height in world metres. When &gt; 0 the text scales with zoom (its pixel size is
    /// GroundHeightMetres / resolution); when 0 the fixed <see cref="FontSize"/> is used.
    /// Set by drag-sizing when the text is placed.
    /// </summary>
    public double GroundHeightMetres { get; set; }

    /// <summary>Background colour behind text (hex), or null for none — text shapes only.</summary>
    public string? BackgroundColorHex { get; set; } = "#FFFFFF";

    // Points in world coordinates (Mapsui units = EPSG:3857 metres)
    public List<(double X, double Y)> Points { get; } = [];

    // For circles: centre + radius in world units
    public (double X, double Y) Centre { get; set; }
    public double RadiusMetres { get; set; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// True when this shape has enough geometry to be a real, keepable drawing:
    /// a line needs 2+ points, a polygon 3+, a circle a radius above the minimum.
    /// Used to decide whether an in-progress shape is committed to the map (on finish
    /// or when the active draw tool changes) or discarded — so a drawn shape is never
    /// silently lost, and a stray click never leaves an empty artifact behind.
    /// </summary>
    public bool IsCompletable(double minCircleRadiusMetres = 50.0) => ShapeType switch
    {
        DrawShapeType.Circle  => RadiusMetres > minCircleRadiusMetres,
        DrawShapeType.Polygon => Points.Count >= 3,
        DrawShapeType.Text    => Points.Count >= 1 && !string.IsNullOrWhiteSpace(Label),
        _                     => Points.Count >= 2,
    };
}
