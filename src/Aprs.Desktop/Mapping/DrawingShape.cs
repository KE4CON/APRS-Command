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
}
