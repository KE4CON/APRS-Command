namespace Aprs.Services;

public sealed class WeatherObservationReceivedEventArgs : EventArgs
{
    public WeatherObservationReceivedEventArgs(
        string driverId,
        CommonWeatherObservation observation,
        DateTimeOffset receivedAtUtc)
    {
        DriverId = driverId;
        Observation = observation;
        ReceivedAtUtc = receivedAtUtc;
    }

    public string DriverId { get; }

    public CommonWeatherObservation Observation { get; }

    public DateTimeOffset ReceivedAtUtc { get; }
}
