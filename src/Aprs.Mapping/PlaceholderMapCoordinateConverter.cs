namespace Aprs.Mapping;

public sealed class PlaceholderMapCoordinateConverter : IMapCoordinateConverter
{
    public MapCoordinate FromNormalizedPoint(double xPercent, double yPercent)
    {
        var x = Math.Clamp(xPercent, 0, 100) / 100;
        var y = Math.Clamp(yPercent, 0, 100) / 100;
        var longitude = (x * 360) - 180;
        var latitude = 90 - (y * 180);

        return new MapCoordinate(latitude, longitude);
    }
}
