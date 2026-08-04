using System;
using System.Collections.Generic;
using static System.FormattableString;
using Mapsui.Projections;

namespace Aprs.Desktop.Mapping;

/// <summary>
/// True-ground length/area of drawn shapes, and human unit formatting.
///
/// Shapes are stored in Web Mercator (EPSG:3857), whose units stretch away from the equator.
/// Multiplying a Mercator distance by cos(latitude) — and a Mercator area by cos²(latitude) —
/// recovers the real ground measurement. Corrections use the segment midpoint (length) or the
/// polygon centroid (area), which is accurate for the small shapes an operator draws on a map.
/// </summary>
public static class ShapeMeasurements
{
    private const double MetresPerFoot = 0.3048;
    private const double FeetPerMile = 5280.0;
    private const double SqMetresPerAcre = 4046.8564224;
    private const double SqMetresPerSqMile = 2_589_988.110336;

    /// <summary>Total ground length of a polyline, in metres.</summary>
    public static double GroundLengthMetres(IReadOnlyList<(double X, double Y)> points)
    {
        double total = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var merc = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            var (_, lat) = SphericalMercator.ToLonLat((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            total += merc * Math.Cos(lat * Math.PI / 180.0);
        }
        return total;
    }

    /// <summary>Ground area (m²) of a closed polygon plus its centroid (Web-Mercator coordinates).</summary>
    public static (double AreaSqMetres, double CentroidX, double CentroidY) GroundAreaAndCentroid(
        IReadOnlyList<(double X, double Y)> points)
    {
        int n = points.Count;
        double shoelace = 0, cx = 0, cy = 0;
        for (int i = 0; i < n; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % n];
            shoelace += a.X * b.Y - b.X * a.Y;
            cx += a.X;
            cy += a.Y;
        }
        cx /= n;
        cy /= n;
        var (_, lat) = SphericalMercator.ToLonLat(cx, cy);
        var c = Math.Cos(lat * Math.PI / 180.0);
        return (Math.Abs(shoelace) / 2.0 * c * c, cx, cy);
    }

    /// <summary>Circle ground radius (m) from its Mercator radius at the given centre.</summary>
    public static double GroundRadiusMetres(double mercatorRadius, double centreX, double centreY)
    {
        var (_, lat) = SphericalMercator.ToLonLat(centreX, centreY);
        return mercatorRadius * Math.Cos(lat * Math.PI / 180.0);
    }

    // small = keep the smaller unit (ft / acres, m / ha) even for large shapes, instead of auto-scaling up.
    public static string FormatLength(double metres, bool imperial, bool small)
    {
        if (imperial)
        {
            var ft = metres / MetresPerFoot;
            return (small || ft < FeetPerMile) ? Invariant($"{ft:N0} ft") : Invariant($"{ft / FeetPerMile:N2} mi");
        }
        return (small || metres < 1000) ? Invariant($"{metres:N0} m") : Invariant($"{metres / 1000.0:N2} km");
    }

    public static string FormatArea(double squareMetres, bool imperial, bool small)
    {
        if (imperial)
        {
            var sqft = squareMetres / (MetresPerFoot * MetresPerFoot);
            if (sqft < 43560) return Invariant($"{sqft:N0} sq ft");
            var acres = squareMetres / SqMetresPerAcre;
            return (small || acres < 640) ? Invariant($"{acres:N2} acres") : Invariant($"{squareMetres / SqMetresPerSqMile:N2} sq mi");
        }
        if (squareMetres < 10000) return Invariant($"{squareMetres:N0} m²");
        var ha = squareMetres / 10000.0;
        return (small || ha < 100) ? Invariant($"{ha:N2} ha") : Invariant($"{squareMetres / 1_000_000.0:N2} km²");
    }
}
