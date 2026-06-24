namespace Aprs.Services;

public interface IAprsWeatherFormatter
{
    AprsWeatherFormatResult FormatPreview(
        CommonWeatherObservation observation,
        LocalStationProfile? localStationProfile = null,
        AprsWeatherFormatterOptions? options = null);
}
