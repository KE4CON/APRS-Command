using System.Globalization;

namespace Aprs.Services;

public static class AprsCoordinateFormatter
{
    public static string FormatLatitude(double latitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees.");
        }

        return FormatCoordinate(latitude, degreeDigits: 2, positiveHemisphere: 'N', negativeHemisphere: 'S');
    }

    public static string FormatLongitude(double longitude)
    {
        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees.");
        }

        return FormatCoordinate(longitude, degreeDigits: 3, positiveHemisphere: 'E', negativeHemisphere: 'W');
    }

    private static string FormatCoordinate(double value, int degreeDigits, char positiveHemisphere, char negativeHemisphere)
    {
        var hemisphere = value >= 0 ? positiveHemisphere : negativeHemisphere;
        var absolute = Math.Abs(value);
        var degrees = (int)Math.Floor(absolute);
        var minutes = Math.Round((absolute - degrees) * 60, 2, MidpointRounding.AwayFromZero);

        if (minutes >= 60)
        {
            degrees++;
            minutes = 0;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{degrees.ToString($"D{degreeDigits}", CultureInfo.InvariantCulture)}{minutes:00.00}{hemisphere}");
    }
}
