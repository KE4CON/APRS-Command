namespace Aprs.Desktop.Mapping;

public enum DrawShapeType { Line, Polygon, Circle }

/// <summary>
/// A user-drawn shape on the APRS map — line, polygon, or circle.
/// Stored in world coordinates (Web Mercator EPSG:3857).
/// </summary>
public sealed class DrawingShape
{
    public Guid Id { get; } = Guid.NewGuid();
    public DrawShapeType ShapeType { get; init; }
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#e63946"; // APRS red
    public double StrokeWidth { get; set; } = 2.0;

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
        _                     => Points.Count >= 2,
    };
}
