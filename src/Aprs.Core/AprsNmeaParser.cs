using System.Globalization;

namespace Aprs.Core;

/// <summary>
/// Parses raw NMEA-0183 GPS sentences carried in APRS <c>$</c> packets (a legacy Data Type
/// Identifier). Recognizes the two common position sentences — <c>GxRMC</c> and <c>GxGGA</c> — and
/// yields a <see cref="PositionAprsPacket"/> (position type <c>$</c>) so a NMEA station plots on the
/// map like any other position. Unrecognized or malformed sentences still return a position packet
/// tagged <c>$</c> (with a validation note and no coordinates), never Unknown.
/// </summary>
public sealed class AprsNmeaParser
{
    private const double FeetPerMeter = 3.280839895;

    /// <summary>
    /// Claims only <c>$</c> packets that are position-bearing NMEA sentences (GxRMC / GxGGA). Other
    /// <c>$</c> data (non-position sentences like GSV, or non-NMEA text) is left for the Unknown path,
    /// since there is no position to surface from it.
    /// </summary>
    public bool CanParse(string information)
    {
        if (!information.StartsWith('$'))
        {
            return false;
        }

        var comma = information.IndexOf(',');
        var header = (comma >= 0 ? information[..comma] : information).ToUpperInvariant();
        return header.EndsWith("RMC", StringComparison.Ordinal)
            || header.EndsWith("GGA", StringComparison.Ordinal);
    }

    public AprsPacket Parse(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var fields = rawPacket.Information.Split(',');
        // e.g. "$GPRMC" -> "RMC"; the two leading talker-ID chars (GP/GN/GL/…) vary.
        var sentence = fields.Length > 0 ? StripChecksum(fields[0]).TrimStart('$').ToUpperInvariant() : string.Empty;

        double? latitude = null;
        double? longitude = null;
        int? courseDegrees = null;
        int? speedKnots = null;
        int? altitudeFeet = null;
        var recognized = false;

        if (sentence.EndsWith("RMC", StringComparison.Ordinal) && fields.Length >= 9)
        {
            // $xxRMC,time,status,lat,N/S,lon,E/W,speed(kn),course,date,...
            latitude = ParseNmeaCoordinate(fields[3], fields[4]);
            longitude = ParseNmeaCoordinate(fields[5], fields[6]);
            speedKnots = ParseRoundedInt(fields[7]);
            courseDegrees = ParseRoundedInt(fields[8]);
            recognized = latitude is not null && longitude is not null;
        }
        else if (sentence.EndsWith("GGA", StringComparison.Ordinal) && fields.Length >= 10)
        {
            // $xxGGA,time,lat,N/S,lon,E/W,fixQuality,sats,hdop,altitude(m),M,...
            latitude = ParseNmeaCoordinate(fields[2], fields[3]);
            longitude = ParseNmeaCoordinate(fields[4], fields[5]);
            altitudeFeet = ParseMetersToFeet(fields[9]);
            recognized = latitude is not null && longitude is not null;
        }

        if (!recognized)
        {
            validationErrors.Add("NMEA sentence is not a recognized position sentence (RMC/GGA) or is malformed.");
        }

        return new PositionAprsPacket(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            rawPacket.Destination,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            rawPacket.IsValid && validationErrors.Count == 0,
            validationErrors,
            rawPacket.QConstruct,
            '$',
            null,
            latitude,
            longitude,
            null,
            null,
            string.Empty,
            courseDegrees,
            speedKnots,
            altitudeFeet,
            0);
    }

    // NMEA coordinate: ddmm.mmmm (lat) / dddmm.mmmm (lon) with a N/S/E/W hemisphere. The minutes are
    // always the two integer digits immediately before the decimal point; everything before that is
    // whole degrees.
    private static double? ParseNmeaCoordinate(string value, string hemisphere)
    {
        value = StripChecksum(value).Trim();
        if (value.Length == 0 || hemisphere.Length == 0)
        {
            return null;
        }

        var dot = value.IndexOf('.');
        var degreeDigits = (dot >= 0 ? dot : value.Length) - 2;
        if (degreeDigits < 1)
        {
            return null;
        }

        if (!int.TryParse(value[..degreeDigits], NumberStyles.Integer, CultureInfo.InvariantCulture, out var degrees)
            || !double.TryParse(value[degreeDigits..], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }

        var result = degrees + minutes / 60.0;
        var h = char.ToUpperInvariant(StripChecksum(hemisphere).Trim()[0]);
        if (h is 'S' or 'W')
        {
            result = -result;
        }

        return result;
    }

    private static int? ParseRoundedInt(string value)
    {
        value = StripChecksum(value).Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? (int)Math.Round(d)
            : null;
    }

    private static int? ParseMetersToFeet(string value)
    {
        value = StripChecksum(value).Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var meters)
            ? (int)Math.Round(meters * FeetPerMeter)
            : null;
    }

    // The final field of a NMEA sentence carries a "*hh" checksum; strip it before numeric parsing.
    private static string StripChecksum(string field)
    {
        var star = field.IndexOf('*');
        return star >= 0 ? field[..star] : field;
    }
}
