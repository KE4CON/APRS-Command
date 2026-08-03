namespace Aprs.Mapping;

public static class MaidenheadGridLocator
{
    public static string FromCoordinates(double latitude, double longitude, int precision = 6)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees.");
        }

        if (precision is not (2 or 4 or 6))
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be 2, 4, or 6 characters.");
        }

        var adjustedLongitude = Math.Min(longitude + 180, 359.999999);
        var adjustedLatitude = Math.Min(latitude + 90, 179.999999);

        var fieldLongitude = (int)(adjustedLongitude / 20);
        var fieldLatitude = (int)(adjustedLatitude / 10);
        var squareLongitude = (int)((adjustedLongitude % 20) / 2);
        var squareLatitude = (int)(adjustedLatitude % 10);
        var subsquareLongitude = (int)(((adjustedLongitude % 2) / 2) * 24);
        var subsquareLatitude = (int)((adjustedLatitude - Math.Floor(adjustedLatitude)) * 24);

        var locator = string.Create(6, (fieldLongitude, fieldLatitude, squareLongitude, squareLatitude, subsquareLongitude, subsquareLatitude), static (span, state) =>
        {
            span[0] = (char)('A' + state.fieldLongitude);
            span[1] = (char)('A' + state.fieldLatitude);
            span[2] = (char)('0' + state.squareLongitude);
            span[3] = (char)('0' + state.squareLatitude);
            span[4] = (char)('A' + state.subsquareLongitude);
            span[5] = (char)('A' + state.subsquareLatitude);
        });

        return locator[..precision];
    }

    /// <summary>
    /// Converts a Maidenhead grid locator to the latitude/longitude of the <b>center</b> of the
    /// grid square. Accepts 4-character (field+square) or 6-character (field+square+subsquare)
    /// locators; longer locators use their first 6 characters.
    /// </summary>
    public static (double Latitude, double Longitude) ToCoordinates(string locator)
    {
        if (string.IsNullOrWhiteSpace(locator) || locator.Length < 4)
        {
            throw new ArgumentException("Locator must be at least 4 characters.", nameof(locator));
        }

        var g = locator.Trim().ToUpperInvariant();

        // Field: 20° of longitude / 10° of latitude per letter (A–R).
        var longitude = (g[0] - 'A') * 20.0 - 180.0;
        var latitude  = (g[1] - 'A') * 10.0 - 90.0;

        // Square: 2° longitude / 1° latitude per digit (0–9).
        longitude += (g[2] - '0') * 2.0;
        latitude  += (g[3] - '0') * 1.0;

        if (g.Length >= 6)
        {
            // Subsquare: 2/24° longitude, 1/24° latitude per letter (A–X), then to its center.
            longitude += (g[4] - 'A') * (2.0 / 24.0) + (1.0 / 24.0);
            latitude  += (g[5] - 'A') * (1.0 / 24.0) + (0.5 / 24.0);
        }
        else
        {
            // Center of the 2°×1° square.
            longitude += 1.0;
            latitude  += 0.5;
        }

        return (latitude, longitude);
    }
}
