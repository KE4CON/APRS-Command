namespace Aprs.Services;

public sealed class WeatherDriverObservationSourceProvider : IWeatherObservationSourceProvider
{
    private readonly IWeatherInputDriverManager driverManager;

    public WeatherDriverObservationSourceProvider(IWeatherInputDriverManager driverManager)
    {
        this.driverManager = driverManager;
    }

    public CommonWeatherObservation? GetLatestObservation(string driverId)
    {
        return driverManager.GetDriver(driverId)?.LastObservation;
    }

    public string? GetSourceName(string driverId)
    {
        var snapshot = driverManager.GetDriver(driverId);
        return snapshot?.LastObservation?.SourceName ?? snapshot?.DriverName;
    }
}
