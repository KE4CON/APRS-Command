using System.Globalization;
using System.Text.RegularExpressions;

namespace Aprs.Services;

public sealed partial class AprsWeatherFormatter : IAprsWeatherFormatter
{
    private readonly WeatherObservationValidator validator;

    public AprsWeatherFormatter()
        : this(new WeatherObservationValidator())
    {
    }

    public AprsWeatherFormatter(WeatherObservationValidator validator)
    {
        this.validator = validator;
    }

    public AprsWeatherFormatResult FormatPreview(
        CommonWeatherObservation observation,
        LocalStationProfile? localStationProfile = null,
        AprsWeatherFormatterOptions? options = null)
    {
        options ??= AprsWeatherFormatterOptions.Default;
        var validation = validator.Validate(observation);
        var errors = validation.Errors.ToList();
        var warnings = validation.Warnings.ToList();

        if (observation.StaleDataState != WeatherDataState.Current)
        {
            errors.Add("Stale weather data cannot be formatted for APRS transmit preview.");
        }

        var source = ResolveSourceStationIdentifier(observation, localStationProfile);
        ValidateStationIdentifier(source, errors);
        ValidateDestination(options.Destination, errors);
        ValidateText(options.Comment, "Comment", errors);

        var latitude = observation.Latitude ?? localStationProfile?.FixedLatitude;
        var longitude = observation.Longitude ?? localStationProfile?.FixedLongitude;
        if (options.UsePosition)
        {
            if (latitude is null)
            {
                errors.Add("Latitude is required for position weather packet preview.");
            }

            if (longitude is null)
            {
                errors.Add("Longitude is required for position weather packet preview.");
            }
        }

        ValidateRequiredWeatherFields(observation, errors);

        if (errors.Count > 0)
        {
            return AprsWeatherFormatResult.Failed(errors.Distinct().ToArray(), warnings);
        }

        var body = options.UsePosition
            ? $"!{AprsCoordinateFormatter.FormatLatitude(latitude!.Value)}/{AprsCoordinateFormatter.FormatLongitude(longitude!.Value)}_{BuildWeatherBody(observation, options.Comment)}"
            : $"_{BuildTimestamp(observation.TimestampUtc)}{BuildWeatherBody(observation, options.Comment)}";
        var packet = $"{source!.Trim().ToUpperInvariant()}>{BuildDestinationAndPath(options.Destination, options.Path)}:{body}";

        return packet.Contains('>') && packet.Contains(':')
            ? AprsWeatherFormatResult.Succeeded(packet, warnings)
            : AprsWeatherFormatResult.Failed(["Generated APRS weather packet is malformed."], warnings);
    }

    private static string BuildWeatherBody(CommonWeatherObservation observation, string? comment)
    {
        var body =
            $"{RoundInt(observation.WindDirectionDegrees):000}/{RoundInt(observation.WindSpeedMph):000}"
            + $"g{RoundInt(observation.WindGustMph):000}"
            + $"t{FormatTemperature(observation.TemperatureFahrenheit!.Value)}"
            + $"r{FormatHundredths(observation.RainLastHourInches)}"
            + $"p{FormatHundredths(observation.RainLast24HoursInches)}"
            + $"P{FormatHundredths(observation.RainSinceMidnightInches)}"
            + $"h{FormatHumidity(observation.HumidityPercent!.Value)}"
            + $"b{RoundInt(observation.BarometricPressureMillibars!.Value * 10):00000}";

        if (observation.LuminosityWattsPerSquareMeter is not null)
        {
            body += $"L{Math.Clamp(RoundInt(observation.LuminosityWattsPerSquareMeter.Value), 0, 999):000}";
        }

        if (observation.SnowInches is not null)
        {
            body += $"s{FormatHundredths(observation.SnowInches)}";
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            body += comment.Trim();
        }

        return body;
    }

    private static void ValidateRequiredWeatherFields(CommonWeatherObservation observation, List<string> errors)
    {
        if (observation.WindDirectionDegrees is null)
        {
            errors.Add("Wind direction is required for APRS weather packet preview.");
        }

        if (observation.WindSpeedMph is null)
        {
            errors.Add("Wind speed is required for APRS weather packet preview.");
        }

        if (observation.WindGustMph is null)
        {
            errors.Add("Wind gust is required for APRS weather packet preview.");
        }

        if (observation.TemperatureFahrenheit is null)
        {
            errors.Add("Temperature is required for APRS weather packet preview.");
        }

        if (observation.HumidityPercent is null)
        {
            errors.Add("Humidity is required for APRS weather packet preview.");
        }

        if (observation.BarometricPressureMillibars is null)
        {
            errors.Add("Barometric pressure is required for APRS weather packet preview.");
        }
    }

    private static string? ResolveSourceStationIdentifier(CommonWeatherObservation observation, LocalStationProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(observation.Callsign))
        {
            return observation.Callsign;
        }

        return profile?.FullStationIdentifier;
    }

    private static void ValidateStationIdentifier(string? sourceStationIdentifier, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sourceStationIdentifier) || !StationIdentifierRegex().IsMatch(sourceStationIdentifier.Trim()))
        {
            errors.Add("Source station identifier must be a valid callsign with optional SSID.");
        }
    }

    private static void ValidateDestination(string destination, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            errors.Add("Destination is required.");
        }

        ValidateText(destination, "Destination", errors);
    }

    private static void ValidateText(string? value, string label, List<string> errors)
    {
        if (value is not null && (value.Contains('\r') || value.Contains('\n')))
        {
            errors.Add($"{label} cannot contain line breaks.");
        }
    }

    private static string BuildDestinationAndPath(string destination, IReadOnlyList<string> path)
    {
        var normalizedDestination = destination.Trim().ToUpperInvariant();
        return path.Count == 0
            ? normalizedDestination
            : $"{normalizedDestination},{string.Join(',', path.Select(component => component.Trim().ToUpperInvariant()))}";
    }

    private static string BuildTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("HHmmss", CultureInfo.InvariantCulture);
    }

    private static int RoundInt(double? value)
    {
        return value is null ? 0 : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private static string FormatTemperature(double fahrenheit)
    {
        var rounded = (int)Math.Round(fahrenheit, MidpointRounding.AwayFromZero);
        return rounded < 0 ? $"-{Math.Abs(rounded):00}" : $"{rounded:000}";
    }

    private static string FormatHundredths(double? inches)
    {
        return Math.Clamp(RoundInt((inches ?? 0) * 100), 0, 999).ToString("000", CultureInfo.InvariantCulture);
    }

    private static string FormatHumidity(int humidity)
    {
        return humidity == 100 ? "00" : humidity.ToString("00", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("^[A-Z0-9]{1,6}(-([0-9]|1[0-5]))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex StationIdentifierRegex();
}
