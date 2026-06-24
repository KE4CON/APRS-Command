namespace Aprs.Services;

internal interface IWeatherInputDriverStatusSink
{
    void SetValidationResult(WeatherObservationValidationResult validationResult);

    void SetStatus(WeatherInputDriverStatus status);
}
