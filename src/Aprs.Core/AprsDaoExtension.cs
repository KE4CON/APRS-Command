namespace Aprs.Core;

/// <summary>
/// Applies the APRS 1.2 <c>!DAO!</c> datum/precision extension (aprs.org/aprs12/datum.txt). A trailing
/// <c>!Dxx!</c> token in a position comment refines the reported latitude/longitude with extra
/// precision beyond the base DDMM.mm minutes: an uppercase datum char carries one human-readable digit
/// (thousandths of a minute, ~6 ft) per coordinate; a lowercase datum char carries a base-91 value
/// (~1 ft); a space datum carries no extra precision. Only a well-formed token at the very end of the
/// comment is recognized (to avoid matching an incidental "!..!" mid-comment), and it is stripped from
/// the returned comment.
/// </summary>
public static class AprsDaoExtension
{
    public static (double? Latitude, double? Longitude, string Comment) Apply(
        double? latitude, double? longitude, string comment)
    {
        if (string.IsNullOrEmpty(comment)) return (latitude, longitude, comment);

        // DAO is conventionally the last token; only accept "!Dxx!" at the end (after trailing space).
        var trimmed = comment.TrimEnd();
        if (trimmed.Length < 5) return (latitude, longitude, comment);
        var index = trimmed.Length - 5;
        if (trimmed[index] != '!' || trimmed[index + 4] != '!') return (latitude, longitude, comment);

        var datum = trimmed[index + 1];
        var latChar = trimmed[index + 2];
        var lonChar = trimmed[index + 3];

        double addLatMinutes;
        double addLonMinutes;
        if (char.IsUpper(datum))
        {
            // Human-readable: one extra decimal digit (thousandths of a minute) per coordinate.
            if (!IsDigit(latChar) || !IsDigit(lonChar)) return (latitude, longitude, comment);
            addLatMinutes = (latChar - '0') / 1000.0;
            addLonMinutes = (lonChar - '0') / 1000.0;
        }
        else if (char.IsLower(datum))
        {
            // Base-91: value 0-90, scaled by 1.1 into the 3rd-4th decimals of a minute (spec datum.txt).
            if (!IsBase91(latChar) || !IsBase91(lonChar)) return (latitude, longitude, comment);
            addLatMinutes = (latChar - 33) * 1.1 / 10000.0;
            addLonMinutes = (lonChar - 33) * 1.1 / 10000.0;
        }
        else if (datum == ' ' && latChar == ' ' && lonChar == ' ')
        {
            // Datum specified with no added precision — valid, but nothing to refine.
            addLatMinutes = 0;
            addLonMinutes = 0;
        }
        else
        {
            return (latitude, longitude, comment); // not a valid DAO token — leave the comment intact
        }

        var refinedLat = Refine(latitude, addLatMinutes);
        var refinedLon = Refine(longitude, addLonMinutes);
        var cleaned = comment[..index].TrimEnd();
        return (refinedLat, refinedLon, cleaned);
    }

    // Adds the extra minutes toward the reported hemisphere (magnitude grows in the same direction).
    private static double? Refine(double? coordinate, double addMinutes)
    {
        if (coordinate is null || addMinutes == 0) return coordinate;
        var sign = coordinate.Value < 0 ? -1.0 : 1.0;
        return coordinate.Value + sign * (addMinutes / 60.0);
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsBase91(char c) => c is >= '!' and <= '{'; // ASCII 33..123 → 0..90
}
