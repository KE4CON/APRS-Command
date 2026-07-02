using System.Globalization;

namespace Aprs.Services;

/// <summary>
/// Encodes APRS area objects per APRS Protocol Reference §11.
/// Area objects use the \l symbol and encode shape data in the comment field.
/// Format: /A{shape-color}{corridor-offset}/{lat}/{lon} or via comment encoding.
/// This encoder generates the comment-field encoding used with the \l symbol.
/// </summary>
public static class AprsAreaObjectEncoder
{
    /// <summary>
    /// Generates the APRS area object comment string that encodes shape, color, and dimensions.
    /// Place this string as the comment field of an APRS object using the \l symbol.
    /// </summary>
    /// <param name="shape">The shape type.</param>
    /// <param name="colorCode">
    /// Color code 0-7: 0=black, 1=blue, 2=green, 3=cyan, 4=red, 5=violet, 6=amber, 7=white.
    /// </param>
    /// <param name="widthKm">Width/radius of the shape in kilometres (0–99).</param>
    /// <param name="heightKm">Height of the shape in kilometres (0–99). Use same as width for circles.</param>
    /// <param name="description">Optional human-readable description appended after the area encoding.</param>
    public static string Encode(
        AprsAreaObjectShape shape,
        int colorCode,
        double widthKm,
        double heightKm,
        string? description = null)
    {
        colorCode = Math.Clamp(colorCode, 0, 7);
        var widthInt  = (int)Math.Clamp(widthKm  * 10, 0, 999);
        var heightInt = (int)Math.Clamp(heightKm * 10, 0, 999);
        var shapeCode = (int)shape;

        // Area encoding: /A{S}{C}{WWW}/{HHH}
        // S = shape digit, C = color digit, WWW = width * 10 (3 digits), HHH = height * 10
        var encoded = $"/A{shapeCode}{colorCode}{widthInt:D3}/{heightInt:D3}";

        return string.IsNullOrWhiteSpace(description)
            ? encoded
            : $"{encoded} {description}";
    }

    /// <summary>
    /// Returns the recommended APRS symbol for area objects: alternate table '\', code 'l'.
    /// </summary>
    public static (char Table, char Code) AreaSymbol => ('\\', 'l');
}
