using System.Globalization;

namespace Aprs.Core;

public sealed class AprsWeatherParser
{
    // Offset from the start of the position data to the symbol code in an uncompressed position:
    // 8 lat chars + 1 symbol-table char + 9 lon chars.
    private const int UncompressedSymbolCodeOffset = 18;

    // A 7-char timestamp (DHM/HMS) follows the type character for '@' and '/' reports, so the
    // position data starts 7 chars later than for the timestampless '!' and '=' reports.
    private const int TimestampLength = 7;

    private const double MphPerKnot = 1.15077945;

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
        int? compressedWindDirection = null;
        int? compressedWindSpeedMph = null;
        string weatherBody;

        if (IsPositionWeather(rawPacket.Information))
        {
            var positionStart = PositionDataStart(rawPacket.Information[0]);

            // '@' and '/' reports carry a 7-char timestamp between the type char and the position.
            if (positionStart > 1 && rawPacket.Information.Length >= 1 + TimestampLength)
                timestamp = rawPacket.Information.Substring(1, TimestampLength);

            if (AprsCompressedPositionDecoder.IsCompressed(rawPacket.Information, positionStart))
            {
                var compressed = AprsCompressedPositionDecoder.Decode(
                    rawPacket.Information, positionStart, "Weather position", validationErrors);
                latitude = compressed.Latitude;
                longitude = compressed.Longitude;
                symbolTableIdentifier = compressed.SymbolTableIdentifier;
                symbolCode = compressed.SymbolCode;
                weatherBody = compressed.Comment;

                // In compressed weather the wind direction/speed ride in the compressed course/speed
                // bytes rather than the c.../s... fields (spec §12); speed is knots -> mph.
                compressedWindDirection = compressed.CourseDegrees;
                if (compressed.SpeedKnots is { } knots)
                {
                    compressedWindSpeedMph = (int)Math.Round(knots * MphPerKnot);
                }
            }
            else
            {
                var parsedPosition = AprsPositionComponents.Parse(
                    rawPacket.Information,
                    positionStart,
                    "Weather position",
                    validationErrors);
                latitude = parsedPosition.Latitude;
                longitude = parsedPosition.Longitude;
                symbolTableIdentifier = parsedPosition.SymbolTableIdentifier;
                symbolCode = parsedPosition.SymbolCode;
                weatherBody = parsedPosition.Comment;
            }
        }
        else
        {
            weatherBody = rawPacket.Information.Length > 1 ? rawPacket.Information[1..] : string.Empty;
            // MDHM timestamp (positionless weather) is 8 digits: MMDDhhmm (spec §6 p.22)
            // HMS and DHM timestamps are 7 chars; MDHM is 8 chars followed by non-digit.
            if (weatherBody.Length >= 8 && weatherBody.Take(8).All(char.IsDigit))
            {
                timestamp = weatherBody[..8]; // MDHM format
                weatherBody = weatherBody[8..];
            }
            else if (weatherBody.Length >= 6 && weatherBody.Take(6).All(char.IsDigit))
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
            parsedWeather.WindDirectionDegrees ?? compressedWindDirection,
            parsedWeather.WindSpeedMph ?? compressedWindSpeedMph,
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

    /// <summary>
    /// The index at which the position data (latitude) begins for a given position-report type char.
    /// '!' and '=' have no timestamp (position at index 1); '@' and '/' carry a 7-char timestamp
    /// first (position at index 8).
    /// </summary>
    private static int PositionDataStart(char typeChar)
        => typeChar is '@' or '/' ? 1 + TimestampLength : 1;

    // In a compressed position the symbol code sits after the symbol-table char + 4 lat + 4 lon bytes.
    private const int CompressedSymbolCodeOffset = 9;

    private static bool IsPositionWeather(string information)
    {
        if (information.Length == 0 || information[0] is not ('!' or '=' or '/' or '@'))
        {
            return false;
        }

        var positionStart = PositionDataStart(information[0]);

        // The weather symbol code ('_') sits right after the position data — at a different offset
        // for compressed vs uncompressed positions. A compressed position with a '_' symbol is a
        // compressed weather report (wind comes from its course/speed bytes).
        var symbolCodeIndex = AprsCompressedPositionDecoder.IsCompressed(information, positionStart)
            ? positionStart + CompressedSymbolCodeOffset
            : positionStart + UncompressedSymbolCodeOffset;

        return information.Length > symbolCodeIndex && information[symbolCodeIndex] == '_';
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
        else if (TryGet(body, index + 3) == '/' && index + 7 <= body.Length)
        {
            // Wind reported as unknown — commonly ".../..." (dots) or spaces. The slot is still 7 chars
            // (DDD/SSS); skip past it so the following fields (gust/temp/rain/humidity/baro) still parse
            // instead of all falling into the comment. Wind stays null. Matches Dire Wolf. (Deep-audit.)
            index += 7;
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
                    // 's' is overloaded in APRS: it is sustained wind speed when the wind speed is not yet
                    // known (the c<dir>s<speed> wind format), otherwise it is 24-hour snowfall. Our
                    // formatter emits wind via the dir/speed prefix, so a later 's' is snow — disambiguate
                    // by position to keep both received traffic and our own round-trip correct.
                    if (windSpeedMph is null)
                    {
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
                    }

                    if (TryReadUnsigned(body, index, 3, out var snowfall))
                    {
                        snow = snowfall;
                        index += 3;
                        parsedFieldCount++;
                        break;
                    }

                    validationErrors.Add("Weather snow is invalid.");
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
                        index += 3; // temperature is always a 3-char field, negative or not
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
                        // Spec §12: 'L' = 0–999 W/m²; lowercase 'l' = value + 1000 (1000–1999).
                        // Previously both were read as the raw 0–999 value (deep-audit, Dire-Wolf confirmed).
                        if (fieldCode == 'l')
                        {
                            luminosity += 1000;
                        }

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
        // APRS temperature is ALWAYS exactly 3 characters (-99..999); a leading minus counts as one of
        // the three. Reading 4 for negatives (the old bug) swallowed the next field's code and made every
        // sub-freezing report fail to parse, dumping temp/humidity/pressure into the comment.
        const int length = 3;
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
