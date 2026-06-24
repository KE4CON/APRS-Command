namespace Aprs.Services;

public sealed class WeatherObservationValidator
{
    private readonly TimeSpan staleThreshold;

    public WeatherObservationValidator(TimeSpan? staleThreshold = null)
    {
        this.staleThreshold = staleThreshold ?? TimeSpan.FromMinutes(15);
    }

    public WeatherObservationValidationResult Validate(CommonWeatherObservation observation, DateTimeOffset? now = null)
    {
        var errors = observation.ValidationErrors.ToList();
        var warnings = observation.ValidationWarnings.ToList();

        if (observation.TimestampUtc == default)
        {
            errors.Add("Weather observation timestamp is required.");
        }

        if (now is not null && observation.TimestampUtc != default && now.Value - observation.TimestampUtc > staleThreshold)
        {
            warnings.Add("Weather observation is stale.");
        }

        if (observation.StaleDataState != WeatherDataState.Current)
        {
            warnings.Add($"Weather observation state is {observation.StaleDataState}.");
        }

        if (observation.Latitude is < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90 degrees.");
        }

        if (observation.Longitude is < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180 degrees.");
        }

        if (observation.WindDirectionDegrees is < 0 or > 360)
        {
            errors.Add("Wind direction must be between 0 and 360 degrees.");
        }

        if (observation.WindSpeedMph is < 0 or > 300)
        {
            errors.Add("Wind speed is outside the supported range.");
        }

        if (observation.WindGustMph is < 0 or > 300)
        {
            errors.Add("Wind gust is outside the supported range.");
        }

        if (observation.TemperatureFahrenheit is < -100 or > 150)
        {
            errors.Add("Temperature is outside the supported range.");
        }

        if (observation.HumidityPercent is < 0 or > 100)
        {
            errors.Add("Humidity must be between 0 and 100 percent.");
        }

        if (observation.BarometricPressureMillibars is < 800 or > 1100)
        {
            errors.Add("Barometric pressure is outside the supported range.");
        }

        if (observation.RainLastHourInches is < 0
            || observation.RainLast24HoursInches is < 0
            || observation.RainSinceMidnightInches is < 0
            || observation.SnowInches is < 0)
        {
            errors.Add("Rain and snow values cannot be negative.");
        }

        if (observation.LightningCount is < 0)
        {
            errors.Add("Lightning count cannot be negative.");
        }

        if (observation.LightningDistanceMiles is < 0)
        {
            errors.Add("Lightning distance cannot be negative.");
        }

        return new WeatherObservationValidationResult(errors.Count == 0, errors, warnings);
    }
}
