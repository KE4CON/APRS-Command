namespace Aprs.Services;

/// <summary>
/// Encodes APRS area objects (BOX / CIRCLE / TRIANGLE / LINE) per the WB4APR spec (aprs.org
/// PROTOCOL.TXT §Area Objects). An area object uses the alternate-table <c>l</c> symbol; the 7 bytes
/// that normally hold the CSE/SPD field instead carry <c>Tyy/Cxx</c>:
/// <list type="bullet">
/// <item><b>T</b> — shape type: 0=circle, 1=line, 3=triangle, 4=box; add 5 for the color-filled forms
/// (5=filled circle, 6=line other-quadrant, 8=filled triangle, 9=filled box).</item>
/// <item><b>yy</b> — latitude half-extent, <b>xx</b> — longitude half-extent, each a two-digit value
/// where the offset in degrees = value² ÷ 100 (i.e. value = 10·√degrees). This √-scaling lets two
/// digits span from well under a mile up to hundreds of miles.</item>
/// <item><b>C</b> — colour digit (0–9).</item>
/// </list>
/// This string is placed as the first bytes of the object's comment (which immediately follow the
/// symbol code), so the receiver reads it from the CSE/SPD position.
/// </summary>
public static class AprsAreaObjectEncoder
{
    private const double KmPerDegree = 111.32; // mean km per degree of latitude

    /// <summary>
    /// Generates the <c>Tyy/Cxx</c> area descriptor (optionally followed by a description). Place it as
    /// the comment of an APRS object using the <see cref="AreaSymbol"/> (alternate table, <c>l</c>).
    /// </summary>
    /// <param name="shape">The shape type.</param>
    /// <param name="colorCode">Colour digit 0–9.</param>
    /// <param name="widthKm">Longitude half-extent (radius for a circle) in kilometres.</param>
    /// <param name="heightKm">Latitude half-extent in kilometres. Use the same as width for circles.</param>
    /// <param name="description">Optional human-readable text appended after the 7-byte descriptor.</param>
    public static string Encode(
        AprsAreaObjectShape shape,
        int colorCode,
        double widthKm,
        double heightKm,
        string? description = null)
    {
        var typeCode = ShapeTypeCode(shape);
        var color = System.Math.Clamp(colorCode, 0, 9);
        var yy = OffsetCode(heightKm); // latitude extent
        var xx = OffsetCode(widthKm);  // longitude extent

        // Tyy/Cxx — exactly 7 bytes, occupying the CSE/SPD position at the start of the comment.
        var descriptor = $"{typeCode}{yy:D2}/{color}{xx:D2}";

        return string.IsNullOrWhiteSpace(description)
            ? descriptor
            : $"{descriptor}{description}";
    }

    /// <summary>The APRS symbol for area objects: alternate table '\', code 'l'.</summary>
    public static (char Table, char Code) AreaSymbol => ('\\', 'l');

    /// <summary>Maps a shape to its APRS area Type code (non-sequential: 0,1,3,4,5,6,8,9).</summary>
    private static int ShapeTypeCode(AprsAreaObjectShape shape) => shape switch
    {
        AprsAreaObjectShape.OpenBlackCircle     => 0,
        AprsAreaObjectShape.OpenRedLine         => 1,
        AprsAreaObjectShape.OpenBlueTriangle    => 3,
        AprsAreaObjectShape.OpenBlueRectangle   => 4,
        AprsAreaObjectShape.FilledBlackCircle   => 5,
        AprsAreaObjectShape.FilledRedLine       => 6,
        AprsAreaObjectShape.FilledBlueTriangle  => 8,
        AprsAreaObjectShape.FilledBlueRectangle => 9,
        _ => 0
    };

    // offset_degrees = code² / 100  →  code = 10 · √degrees, clamped to the two-digit 0–99 range.
    private static int OffsetCode(double km)
    {
        if (km <= 0) return 0;
        var degrees = km / KmPerDegree;
        var code = (int)System.Math.Round(10.0 * System.Math.Sqrt(degrees));
        return System.Math.Clamp(code, 0, 99);
    }
}
