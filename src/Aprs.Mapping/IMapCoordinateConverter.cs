namespace Aprs.Mapping;

public interface IMapCoordinateConverter
{
    MapCoordinate FromNormalizedPoint(double xPercent, double yPercent);
}
