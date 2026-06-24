namespace Aprs.Services;

public sealed record SimulatedMobileStation(
    string Callsign,
    double Latitude,
    double Longitude,
    double SpeedKnots,
    double CourseDegrees);
