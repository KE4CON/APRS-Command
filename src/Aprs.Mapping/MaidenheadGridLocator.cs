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
}
