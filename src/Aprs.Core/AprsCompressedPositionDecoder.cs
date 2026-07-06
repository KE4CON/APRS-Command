using System.Globalization;

namespace Aprs.Core;

/// <summary>
/// Decodes APRS compressed position format as defined in
/// APRS Protocol Reference §9 (pages 36–41).
///
/// Compressed format layout (13 characters):
///   /YYYYXXXX$csT
/// where:
///   /    = Symbol Table Identifier (/ or \)
///   YYYY = 4 base-91 bytes encoding latitude
///   XXXX = 4 base-91 bytes encoding longitude
///   $    = Symbol Code
///   cs   = 2 base-91 bytes: course/speed, radio range, or altitude
///   T    = 1 compression type byte
///
/// Detection: a position field starts with compressed data when the
/// character at the latitude-start position is a Symbol Table Identifier
/// (/ or \) rather than a digit (0–9). Normal lat/long always starts
/// with two digit characters (degrees).
/// </summary>
internal static class AprsCompressedPositionDecoder
{
    // Spec §9 p.38: YYYY = 380926 × (90 − latitude) [base 91]
    private const double LatMultiplier = 380926.0;

    // Spec §9 p.38: XXXX = 190463 × (180 + longitude) [base 91]
    private const double LonMultiplier = 190463.0;

    /// <summary>
    /// Returns true when the character at <paramref name="offset"/> indicates
    /// compressed position data rather than normal lat/long data.
    /// </summary>
    public static bool IsCompressed(string information, int offset)
    {
        if (offset >= information.Length) return false;
        var c = information[offset];
        // Compressed format leads with the Symbol Table Identifier (/ or \).
        // Normal lat/long leads with digit characters (degree digits).
        return c is '/' or '\\';
    }

    /// <summary>
    /// Decodes compressed position data from <paramref name="information"/>
    /// starting at <paramref name="offset"/>.
    /// </summary>
    public static AprsCompressedPosition Decode(
        string information,
        int offset,
        string errorPrefix,
        List<string> validationErrors)
    {
        // Minimum: 1 (STI) + 4 (YYYY) + 4 (XXXX) + 1 ($) + 2 (cs) + 1 (T) = 13 chars
        const int MinLength = 13;

        if (information.Length < offset + MinLength)
        {
            validationErrors.Add(
                $"{errorPrefix} compressed position field is too short " +
                $"(expected ≥ {MinLength} chars from offset {offset}, " +
                $"got {Math.Max(0, information.Length - offset)}).");
            return AprsCompressedPosition.Invalid;
        }

        // ── Symbol Table Identifier ──────────────────────────────────────
        var symbolTableIdentifier = information[offset];

        // ── YYYY: compressed latitude (4 base-91 chars) ──────────────────
        var y1 = Base91Value(information[offset + 1]);
        var y2 = Base91Value(information[offset + 2]);
        var y3 = Base91Value(information[offset + 3]);
        var y4 = Base91Value(information[offset + 4]);

        if (y1 < 0 || y2 < 0 || y3 < 0 || y4 < 0)
        {
            validationErrors.Add($"{errorPrefix} compressed latitude contains invalid base-91 character.");
            return AprsCompressedPosition.Invalid;
        }

        // Spec §9 p.38: Lat = 90 - ((y1×91³ + y2×91² + y3×91 + y4) / 380926)
        var latValue = (y1 * 91.0 * 91.0 * 91.0)
                     + (y2 * 91.0 * 91.0)
                     + (y3 * 91.0)
                     +  y4;
        var latitude = 90.0 - (latValue / LatMultiplier);

        // ── XXXX: compressed longitude (4 base-91 chars) ─────────────────
        var x1 = Base91Value(information[offset + 5]);
        var x2 = Base91Value(information[offset + 6]);
        var x3 = Base91Value(information[offset + 7]);
        var x4 = Base91Value(information[offset + 8]);

        if (x1 < 0 || x2 < 0 || x3 < 0 || x4 < 0)
        {
            validationErrors.Add($"{errorPrefix} compressed longitude contains invalid base-91 character.");
            return AprsCompressedPosition.Invalid;
        }

        // Spec §9 p.38: Long = -180 + ((x1×91³ + x2×91² + x3×91 + x4) / 190463)
        var lonValue = (x1 * 91.0 * 91.0 * 91.0)
                     + (x2 * 91.0 * 91.0)
                     + (x3 * 91.0)
                     +  x4;
        var longitude = -180.0 + (lonValue / LonMultiplier);

        // ── Symbol Code ──────────────────────────────────────────────────
        var symbolCode = information[offset + 9];

        // ── cs bytes (course/speed, radio range, or altitude) ────────────
        var cByte = information[offset + 10];
        var sByte = information[offset + 11];

        // ── T byte (compression type) ─────────────────────────────────────
        var tByte = information[offset + 12];

        // ── Comment (everything after the 13-byte compressed block) ──────
        var commentStart = offset + 13;
        var comment = commentStart < information.Length
            ? information[commentStart..]
            : string.Empty;

        // ── Decode cs bytes ───────────────────────────────────────────────
        int? courseDegrees = null;
        int? speedKnots   = null;
        int? altitudeFeet  = null;
        int? radioRangeMiles = null;

        // Spec §9 p.38: if c = V (space, ASCII 0x20), csT bytes carry no data.
        // The spec uses V as notation for space; on-air this should be 0x20.
        // Some implementations send the literal letter 'V' per the spec notation,
        // so we treat both as "no data".
        if (cByte != ' ' && cByte != 'V')
        {
            var cVal = Base91Value(cByte);
            var sVal = Base91Value(sByte);

            if (cByte == '{')
            {
                // Spec §9 p.39: c = { means pre-calculated radio range
                // range = 2 × 1.08^s
                if (sVal >= 0)
                    radioRangeMiles = (int)Math.Round(2.0 * Math.Pow(1.08, sVal));
            }
            else if (IsGgaSentence(tByte))
            {
                // Spec §9 p.40: when T byte bits 4-3 = 10 (GGA source),
                // cs bytes contain altitude: altitude = 1.002^cs feet
                var csVal = cVal * 91 + sVal;
                if (csVal >= 0)
                    altitudeFeet = (int)Math.Round(Math.Pow(1.002, csVal));
            }
            else if (cVal >= 0 && cVal <= 89)
            {
                // Spec §9 p.38-39: c in range 0-89 → course/speed
                // course = c × 4   (degrees)
                // speed  = 1.08^s - 1   (knots)
                courseDegrees = cVal * 4;
                if (sVal >= 0)
                    speedKnots = (int)Math.Round(Math.Pow(1.08, sVal) - 1.0);
            }
        }

        // Also parse /A=altitude from comment (may supplement cs altitude)
        if (altitudeFeet is null)
            altitudeFeet = AprsPositionComponents.ParseAltitude(comment);

        return new AprsCompressedPosition(
            latitude, longitude,
            symbolTableIdentifier, symbolCode,
            courseDegrees, speedKnots,
            altitudeFeet, radioRangeMiles,
            comment);
    }

    /// <summary>
    /// Converts a base-91 ASCII character to its numeric value (0–90)
    /// by subtracting 33 from the character code.
    /// Returns -1 for characters outside the valid range 33–124 (!...|).
    /// </summary>
    private static int Base91Value(char c)
    {
        var v = (int)c - 33;
        return v is >= 0 and <= 90 ? v : -1;
    }

    /// <summary>
    /// Spec §9 p.39-40: bits 4-3 of T byte = 10 means the NMEA source
    /// was a GGA sentence, and the cs bytes contain altitude data.
    /// </summary>
    private static bool IsGgaSentence(char tByte)
    {
        var tVal = (int)tByte - 33;
        return tVal >= 0 && ((tVal >> 3) & 0b11) == 0b10;
    }
}

/// <summary>Decoded result of a compressed position field.</summary>
internal sealed record AprsCompressedPosition(
    double? Latitude,
    double? Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    int? CourseDegrees,
    int? SpeedKnots,
    int? AltitudeFeet,
    int? RadioRangeMiles,
    string Comment)
{
    /// <summary>Sentinel returned when decoding fails.</summary>
    public static AprsCompressedPosition Invalid { get; } =
        new(null, null, null, null, null, null, null, null, string.Empty);
}
