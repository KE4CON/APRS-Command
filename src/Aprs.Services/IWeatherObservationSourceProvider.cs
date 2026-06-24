namespace Aprs.Services;

public interface IWeatherObservationSourceProvider
{
    CommonWeatherObservation? GetLatestObservation(string driverId);

    string? GetSourceName(string driverId);
}
