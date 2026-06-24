namespace Aprs.Services;

public interface IWeatherBeaconScheduler
{
    WeatherBeaconSchedulerState GetState();

    WeatherBeaconSchedulerState Start();

    WeatherBeaconSchedulerState Stop();

    WeatherBeaconSchedulerState SelectWeatherSource(string driverId);

    WeatherBeaconPreviewResult GeneratePreview(WeatherBeaconTransmitTransport? preferredTransport = null);

    Task<WeatherBeaconTransmitResult> TransmitWeatherNowAsync(
        WeatherBeaconTransmitTransport destinationTransport,
        CancellationToken cancellationToken = default);

    Task<WeatherBeaconTransmitResult?> TickAsync(CancellationToken cancellationToken = default);
}
