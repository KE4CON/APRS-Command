using System.Globalization;

namespace Aprs.Core;

internal sealed record AprsParsedPosition(
    double? Latitude,
    double? Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    string Comment,
    int PositionAmbiguity);

internal static class AprsPositionComponents
{
    public static AprsParsedPosition Parse(
        string information,
        int latitudeStart,
        string errorPrefix,
        List<string> validationErrors)
    {
        var latitudeText = SliceOrEmpty(information, latitudeStart, 8);
        var symbolTableIndex = latitudeStart + 8;
        var symbolTableIdentifier = TryGetChar(information, symbolTableIndex);
        var longitudeStart = symbolTableIndex + 1;
        var longitudeText = SliceOrEmpty(information, longitudeStart, 9);
        var symbolCodeIndex = longitudeStart + 9;
        var symbolCode = TryGetChar(information, symbolCodeIndex);
        var commentStart = symbolCodeIndex + 1;
        var comment = commentStart <= information.Length ? information[commentStart..] : string.Empty;

        if (latitudeText.Length != 8)
        {
            validationErrors.Add($"{errorPrefix} latitude is missing or incomplete.");
        }

        if (longitudeText.Length != 9)
        {
            validationErrors.Add($"{errorPrefix} longitude is missing or incomplete.");
        }

        if (symbolTableIdentifier is null)
        {
            validationErrors.Add($"{errorPrefix} symbol table identifier is missing.");
        }

        if (symbolCode is null)
        {
            validationErrors.Add($"{errorPrefix} symbol code is missing.");
        }

        var latitude = TryParseLatitude(latitudeText, errorPrefix, validationErrors);
        var longitude = TryParseLongitude(longitudeText, errorPrefix, validationErrors);
        var positionAmbiguity = CountPositionAmbiguity(latitudeText, longitudeText);

        return new AprsParsedPosition(
            latitude,
            longitude,
            symbolTableIdentifier,
            symbolCode,
            comment,
            positionAmbiguity);
    }

    public static (int? CourseDegrees, int? SpeedKnots) ParseCourseAndSpeed(string comment)
    {
        if (comment.Length < 7
            || !comment.Take(3).All(char.IsDigit)
            || comment[3] != '/'
            || !comment.Skip(4).Take(3).All(char.IsDigit))
        {
            return (null, null);
        }

        var course = int.Parse(comment[..3], CultureInfo.InvariantCulture);
        var speed = int.Parse(comment.Substring(4, 3), CultureInfo.InvariantCulture);

        return course <= 360 ? (course, speed) : (null, speed);
    }

    public static int? ParseAltitude(string comment)
    {
        var altitudeIndex = comment.IndexOf("/A=", StringComparison.Ordinal);
        if (altitudeIndex < 0 || altitudeIndex + 9 > comment.Length)
        {
            return null;
        }

        var altitudeText = comment.Substring(altitudeIndex + 3, 6);
        return int.TryParse(altitudeText, NumberStyles.None, CultureInfo.InvariantCulture, out var altitude)
            ? altitude
            : null;
    }

    private static string SliceOrEmpty(string value, int start, int length)
    {
        if (start >= value.Length)
        {
            return string.Empty;
        }

        return value.Substring(start, Math.Min(length, value.Length - start));
    }

    private static char? TryGetChar(string value, int index)
    {
        return index >= 0 && index < value.Length ? value[index] : null;
    }

    private static double? TryParseLatitude(string latitudeText, string errorPrefix, List<string> validationErrors)
    {
        if (latitudeText.Length != 8)
        {
            return null;
        }

        var hemisphere = latitudeText[7];
        if (hemisphere is not ('N' or 'S'))
        {
            validationErrors.Add($"{errorPrefix} latitude hemisphere is invalid.");
            return null;
        }

        var normalized = latitudeText[..7].Replace(' ', '0');
        if (!int.TryParse(normalized[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var degrees)
            || !double.TryParse(normalized[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minutes)
            || degrees > 90
            || minutes >= 60)
        {
            validationErrors.Add($"{errorPrefix} latitude is invalid.");
            return null;
        }

        var decimalDegrees = degrees + minutes / 60;
        return hemisphere == 'S' ? -decimalDegrees : decimalDegrees;
    }

    private static double? TryParseLongitude(string longitudeText, string errorPrefix, List<string> validationErrors)
    {
        if (longitudeText.Length != 9)
        {
            return null;
        }

        var hemisphere = longitudeText[8];
        if (hemisphere is not ('E' or 'W'))
        {
            validationErrors.Add($"{errorPrefix} longitude hemisphere is invalid.");
            return null;
        }

        var normalized = longitudeText[..8].Replace(' ', '0');
        if (!int.TryParse(normalized[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var degrees)
            || !double.TryParse(normalized[3..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var minutes)
            || degrees > 180
            || minutes >= 60)
        {
            validationErrors.Add($"{errorPrefix} longitude is invalid.");
            return null;
        }

        var decimalDegrees = degrees + minutes / 60;
        return hemisphere == 'W' ? -decimalDegrees : decimalDegrees;
    }

    private static int CountPositionAmbiguity(string latitudeText, string longitudeText)
    {
        var latitudeAmbiguity = latitudeText.Take(7).Count(character => character == ' ');
        var longitudeAmbiguity = longitudeText.Take(8).Count(character => character == ' ');

        return Math.Max(latitudeAmbiguity, longitudeAmbiguity);
    }
}
