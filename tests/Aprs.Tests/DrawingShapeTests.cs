using Aprs.Desktop.Mapping;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Regression tests for the draw-tools "never lose a drawn shape" rule.
///
/// Bug: switching draw tools (or finishing) used to discard the in-progress line/polygon
/// because it was never committed to the completed-shapes list. The commit decision now
/// runs through <see cref="DrawingShape.IsCompletable"/>; these tests pin that decision so
/// a real shape is always kept and a stray click never leaves an empty artifact.
/// </summary>
public class DrawingShapeTests
{
    private static DrawingShape Line(params (double X, double Y)[] pts)
    {
        var s = new DrawingShape { ShapeType = DrawShapeType.Line };
        foreach (var p in pts) s.Points.Add(p);
        return s;
    }

    private static DrawingShape Polygon(params (double X, double Y)[] pts)
    {
        var s = new DrawingShape { ShapeType = DrawShapeType.Polygon };
        foreach (var p in pts) s.Points.Add(p);
        return s;
    }

    [Fact]
    public void Line_WithFewerThanTwoPoints_IsNotCompletable()
    {
        Assert.False(Line().IsCompletable());
        Assert.False(Line((0, 0)).IsCompletable());
    }

    [Fact]
    public void Line_WithTwoOrMorePoints_IsCompletable()
    {
        Assert.True(Line((0, 0), (100, 100)).IsCompletable());
        Assert.True(Line((0, 0), (100, 100), (200, 50)).IsCompletable());
    }

    [Fact]
    public void Polygon_NeedsAtLeastThreePoints()
    {
        Assert.False(Polygon((0, 0), (100, 0)).IsCompletable());
        Assert.True(Polygon((0, 0), (100, 0), (100, 100)).IsCompletable());
    }

    [Fact]
    public void Circle_IsCompletableOnlyAboveMinimumRadius()
    {
        var tiny = new DrawingShape { ShapeType = DrawShapeType.Circle, Centre = (0, 0), RadiusMetres = 10 };
        var real = new DrawingShape { ShapeType = DrawShapeType.Circle, Centre = (0, 0), RadiusMetres = 500 };

        Assert.False(tiny.IsCompletable(minCircleRadiusMetres: 50));  // a bare click, no drag
        Assert.True(real.IsCompletable(minCircleRadiusMetres: 50));   // a real drag-out
    }

    [Fact]
    public void Text_NeedsAnAnchorPointAndNonEmptyLabel()
    {
        var noPoint = new DrawingShape { ShapeType = DrawShapeType.Text, Label = "Staging" };
        var noText  = new DrawingShape { ShapeType = DrawShapeType.Text };
        noText.Points.Add((0, 0));
        var ok = new DrawingShape { ShapeType = DrawShapeType.Text, Label = "Staging" };
        ok.Points.Add((0, 0));

        Assert.False(noPoint.IsCompletable());                 // label but never placed
        Assert.False(noText.IsCompletable());                  // placed but blank
        Assert.False(new DrawingShape { ShapeType = DrawShapeType.Text }.IsCompletable());
        Assert.True(ok.IsCompletable());
    }

    [Fact]
    public void NewShape_HasSensibleStyleDefaults()
    {
        var s = new DrawingShape { ShapeType = DrawShapeType.Polygon };
        Assert.Equal(DrawFillStyle.Solid, s.FillStyle);   // polygons default to solid tint
        Assert.Equal("#FF0000", s.Color);                 // default vivid red
        Assert.Equal(3.0, s.StrokeWidth);                 // bold enough to read true colour
        Assert.Equal(14.0, s.FontSize);                   // default text size
    }
}
