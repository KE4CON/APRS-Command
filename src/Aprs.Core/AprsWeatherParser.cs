using System.Globalization;

namespace Aprs.Core;

public sealed class AprsWeatherParser
{
    private const int PositionWeatherSymbolCodeIndex = 19;

    public bool CanParse(string information)
    {
        return information.StartsWith('_')
            || IsPositionWeather(information);
    }

    public WeatherAprsPacket Parse(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        double? latitude = null;
        double? longitude = null;
        char? symbolTableIdentifier = null;
        char? symbolCode = null;
        string? timestamp = null;
        string weatherBody;

        if (IsPositionWeather(rawPacket.Information))
        {
            var parsedPosition = AprsPositionComponents.Parse(
                rawPacket.Information,
                1,
                "Weather position",
                validationErrors);
            latitude = parsedPosition.Latitude;
            longitude = parsedPosition.Longitude;
            symbolTableIdentifier = parsedPosition.SymbolTableIdentifier;
            symbolCode = parsedPosition.SymbolCode;
            weatherBody = parsedPosition.Comment;
        }
        else
        {
            weatherBody = rawPacket.Information.Length > 1 ? rawPacket.Information[1..] : string.Empty;
            if (weatherBody.Length >= 6 && weatherBody.Take(6).All(char.IsDigit))
            {
                timestamp = weatherBody[..6];
                weatherBody = weatherBody[6..];
            }
        }

        var parsedWeather = ParseWeatherBody(weatherBody, validationErrors);

        return new WeatherAprsPacket(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            rawPacket.Destination,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            rawPacket.IsValid && validationErrors.Count == 0,
            validationErrors,
            rawPacket.QConstruct,
            latitude,
            longitude,
            symbolTableIdentifier,
            symbolCode,
            timestamp,
            weatherBody,
            parsedWeather.WindDirectionDegrees,
            parsedWeather.WindSpeedMph,
            parsedWeather.WindGustMph,
            parsedWeather.TemperatureFahrenheit,
            parsedWeather.RainLastHourHundredthsInch,
            parsedWeather.RainLast24HoursHundredthsInch,
            parsedWeather.RainSinceMidnightHundredthsInch,
            parsedWeather.HumidityPercent,
            parsedWeather.BarometricPressureMillibars,
            parsedWeather.LuminosityWattsPerSquareMeter,
            parsedWeather.SnowHundredthsInch,
            parsedWeather.Comment);
    }

    private static bool IsPositionWeather(string information)
    {
        return information.Length > PositionWeatherSymbolCodeIndex
            && information[0] is '!' or '=' or '/' or '@'
            && information[PositionWeatherSymbolCodeIndex] == '_';
    }

    private static ParsedWeatherValues ParseWeatherBody(string body, List<string> validationErrors)
    {
        var index = 0;
        var parsedFieldCount = 0;
        int? windDirectionDegrees = null;
        int? windSpeedMph = null;
        int? windGustMph = null;
        int? temperatureFahrenheit = null;
        int? rainLastHour = null;
        int? rainLast24Hours = null;
        int? rainSinceMidnight = null;
        int? humidityPercent = null;
        double? pressureMillibars = null;
        int? luminosity = null;
        int? snow = null;

        if (TryParseFixedInt(body, index, 3, out var compactWindDirection)
            && TryGet(body, index + 3) == '/'
            && TryParseFixedInt(body, index + 4, 3, out var compactWindSpeed))
        {
            windDirectionDegrees = compactWindDirection;
            windSpeedMph = compactWindSpeed;
            index += 7;
            parsedFieldCount += 2;
        }
        else if (TryGet(body, index) == 'c')
        {
            index++;
            if (TryParseFixedInt(body, index, 3, out var direction))
            {
                windDirectionDegrees = direction;
                index += 3;
                parsedFieldCount++;
            }
            else
            {
                validationErrors.Add("Weather wind direction is invalid.");
            }
        }

        while (index < body.Length)
        {
            var fieldCode = body[index];
            var fieldStart = index;
            index++;

            switch (fieldCode)
            {
                case 's':
                    if (TryReadUnsigned(body, index, 3, out var windSpeed))
                    {
                        windSpeedMph = windSpeed;
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather wind speed is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'g':
                    if (TryReadUnsigned(body, index, 3, out windGustMph))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather wind gust is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 't':
                    if (TryReadSignedTemperature(body, index, out temperatureFahrenheit))
                    {
                        index += body[index] == '-' ? 4 : 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather temperature is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'r':
                    if (TryReadUnsigned(body, index, 3, out rainLastHour))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather rain last hour is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'p':
                    if (TryReadUnsigned(body, index, 3, out rainLast24Hours))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather rain last 24 hours is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'P':
                    if (TryReadUnsigned(body, index, 3, out rainSinceMidnight))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather rain since midnight is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'h':
                    if (TryReadUnsigned(body, index, 2, out humidityPercent))
                    {
                        humidityPercent = humidityPercent == 0 ? 100 : humidityPercent;
                        index += 2;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather humidity is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'b':
                    if (TryReadUnsigned(body, index, 5, out var pressureTenths))
                    {
                        pressureMillibars = pressureTenths / 10.0;
                        index += 5;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather barometric pressure is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'L':
                case 'l':
                    if (TryReadUnsigned(body, index, 3, out luminosity))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather luminosity is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                case 'S':
                    if (TryReadUnsigned(body, index, 3, out snow))
                    {
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather snow is invalid.");
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
                default:
                    index = fieldStart;
                    return Finish(body, index, parsedFieldCount, validationErrors);
            }
        }

        return Finish(body, index, parsedFieldCount, validationErrors);

        ParsedWeatherValues Finish(
            string weatherBody,
            int commentStart,
            int fields,
            List<string> errors)
        {
            if (fields == 0)
            {
                errors.Add("Weather packet contains no recognized weather fields.");
            }

            return new ParsedWeatherValues(
                windDirectionDegrees,
                windSpeedMph,
                windGustMph,
                temperatureFahrenheit,
                rainLastHour,
                rainLast24Hours,
                rainSinceMidnight,
                humidityPercent,
                pressureMillibars,
                luminosity,
                snow,
                commentStart < weatherBody.Length ? weatherBody[commentStart..] : string.Empty);
        }
    }

    private static char? TryGet(string value, int index)
    {
        return index >= 0 && index < value.Length ? value[index] : null;
    }

    private static bool TryParseFixedInt(string value, int start, int length, out int result)
    {
        result = default;
        if (start < 0 || start + length > value.Length)
        {
            return false;
        }

        var slice = value.Substring(start, length);
        return slice.All(char.IsDigit)
            && int.TryParse(slice, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryReadUnsigned(string value, int start, int length, out int? result)
    {
        result = null;
        if (!TryParseFixedInt(value, start, length, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryReadSignedTemperature(string value, int start, out int? result)
    {
        result = null;
        if (start >= value.Length)
        {
            return false;
        }

        var length = value[start] == '-' ? 4 : 3;
        if (start + length > value.Length)
        {
            return false;
        }

        var slice = value.Substring(start, length);
        if (!int.TryParse(slice, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private sealed record ParsedWeatherValues(
        int? WindDirectionDegrees,
        int? WindSpeedMph,
        int? WindGustMph,
        int? TemperatureFahrenheit,
        int? RainLastHourHundredthsInch,
        int? RainLast24HoursHundredthsInch,
        int? RainSinceMidnightHundredthsInch,
        int? HumidityPercent,
        double? BarometricPressureMillibars,
        int? LuminosityWattsPerSquareMeter,
        int? SnowHundredthsInch,
        string Comment);
}
