using System.Globalization;

namespace Aprs.Services;

public sealed class NmeaParser : INmeaParser
{
    public NmeaParseResult Parse(string rawSentence, string sourceName = "NMEA", DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(rawSentence))
        {
            return NmeaParseResult.Failed("Unknown", null, ["NMEA sentence is empty."]);
        }

        var trimmed = rawSentence.Trim();
        if (!trimmed.StartsWith('$'))
        {
            return NmeaParseResult.Failed("Unknown", null, ["NMEA sentence must start with '$'."]);
        }

        var payloadWithChecksum = trimmed[1..];
        string payload;
        bool? checksumValid = null;
        var checksumIndex = payloadWithChecksum.IndexOf('*', StringComparison.Ordinal);
        if (checksumIndex >= 0)
        {
            payload = payloadWithChecksum[..checksumIndex];
            var providedChecksum = payloadWithChecksum[(checksumIndex + 1)..];
            checksumValid = IsChecksumValid(payload, providedChecksum);
            if (checksumValid == false)
            {
                warnings.Add("NMEA checksum does not match.");
            }
        }
        else
        {
            payload = payloadWithChecksum;
            warnings.Add("NMEA sentence has no checksum.");
        }

        var fields = payload.Split(',');
        if (fields.Length == 0 || fields[0].Length < 3)
        {
            return NmeaParseResult.Failed("Unknown", checksumValid, ["NMEA sentence type is missing."], warnings);
        }

        var sentenceType = fields[0][^3..].ToUpperInvariant();
        return sentenceType switch
        {
            "GGA" => ParseGga(fields, sourceName, trimmed, receivedAt, checksumValid, warnings),
            "RMC" => ParseRmc(fields, sourceName, trimmed, receivedAt, checksumValid, warnings),
            _ => NmeaParseResult.Failed(sentenceType, checksumValid, [$"Unsupported NMEA sentence type '{sentenceType}'."], warnings)
        };
    }

    private static NmeaParseResult ParseGga(
        string[] fields,
        string sourceName,
        string rawSentence,
        DateTimeOffset receivedAt,
        bool? checksumValid,
        IReadOnlyList<string> warnings)
    {
        var errors = new List<string>();
        var latitude = ParseCoordinate(GetField(fields, 2), GetField(fields, 3), isLatitude: true, errors);
        var longitude = ParseCoordinate(GetField(fields, 4), GetField(fields, 5), isLatitude: false, errors);
        var fixQuality = ParseNullableInt(GetField(fields, 6), "fix quality", errors);
        var satelliteCount = ParseNullableInt(GetField(fields, 7), "satellite count", errors);
        var hdop = ParseNullableDouble(GetField(fields, 8), "HDOP", errors);
        var altitude = ParseNullableDouble(GetField(fields, 9), "altitude", errors);
        var timestamp = ParseTimeWithReceivedDate(GetField(fields, 1), receivedAt, errors);

        var fixValid = fixQuality.GetValueOrDefault() > 0 && latitude is not null && longitude is not null;
        if (fixQuality.GetValueOrDefault() == 0)
        {
            errors.RemoveAll(error => error.Contains("latitude", StringComparison.OrdinalIgnoreCase)
                || error.Contains("longitude", StringComparison.OrdinalIgnoreCase));
        }

        var position = new GpsPosition(
            latitude,
            longitude,
            altitude,
            SpeedKnots: null,
            CourseDegrees: null,
            timestamp,
            fixValid,
            fixQuality,
            satelliteCount,
            hdop,
            sourceName,
            rawSentence,
            receivedAt);

        return new NmeaParseResult(
            IsParsed: errors.Count == 0 || fixQuality.GetValueOrDefault() == 0,
            SentenceType: "GGA",
            Position: errors.Count == 0 || fixQuality.GetValueOrDefault() == 0 ? position : null,
            ChecksumValid: checksumValid,
            Errors: errors,
            Warnings: warnings);
    }

    private static NmeaParseResult ParseRmc(
        string[] fields,
        string sourceName,
        string rawSentence,
        DateTimeOffset receivedAt,
        bool? checksumValid,
        IReadOnlyList<string> warnings)
    {
        var errors = new List<string>();
        var status = GetField(fields, 2).ToUpperInvariant();
        var latitude = ParseCoordinate(GetField(fields, 3), GetField(fields, 4), isLatitude: true, errors);
        var longitude = ParseCoordinate(GetField(fields, 5), GetField(fields, 6), isLatitude: false, errors);
        var speed = ParseNullableDouble(GetField(fields, 7), "speed", errors);
        var course = ParseNullableDouble(GetField(fields, 8), "course", errors);
        var timestamp = ParseDateTime(GetField(fields, 1), GetField(fields, 9), receivedAt, errors);

        var fixValid = string.Equals(status, "A", StringComparison.OrdinalIgnoreCase)
            && latitude is not null
            && longitude is not null;
        if (!string.Equals(status, "A", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "V", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("RMC status must be A or V.");
        }

        var position = new GpsPosition(
            latitude,
            longitude,
            AltitudeMeters: null,
            speed,
            course,
            timestamp,
            fixValid,
            FixQuality: null,
            SatelliteCount: null,
            Hdop: null,
            sourceName,
            rawSentence,
            receivedAt);

        return new NmeaParseResult(
            IsParsed: errors.Count == 0,
            SentenceType: "RMC",
            Position: errors.Count == 0 ? position : null,
            ChecksumValid: checksumValid,
            Errors: errors,
            Warnings: warnings);
    }

    private static double? ParseCoordinate(string value, string hemisphere, bool isLatitude, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(hemisphere))
        {
            errors.Add($"{(isLatitude ? "Latitude" : "Longitude")} is missing.");
            return null;
        }

        var degreeDigits = isLatitude ? 2 : 3;
        if (value.Length <= degreeDigits
            || !double.TryParse(value[degreeDigits..], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(value[..degreeDigits], NumberStyles.Integer, CultureInfo.InvariantCulture, out var degrees))
        {
            errors.Add($"{(isLatitude ? "Latitude" : "Longitude")} coordinate is malformed.");
            return null;
        }

        var decimalDegrees = degrees + (minutes / 60d);
        var normalizedHemisphere = hemisphere.Trim().ToUpperInvariant();
        if (normalizedHemisphere is "S" or "W")
        {
            decimalDegrees *= -1;
        }
        else if (normalizedHemisphere is not ("N" or "E"))
        {
            errors.Add($"{(isLatitude ? "Latitude" : "Longitude")} hemisphere is malformed.");
            return null;
        }

        if (isLatitude && decimalDegrees is < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90 degrees.");
            return null;
        }

        if (!isLatitude && decimalDegrees is < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180 degrees.");
            return null;
        }

        return decimalDegrees;
    }

    private static DateTimeOffset? ParseTimeWithReceivedDate(string timeValue, DateTimeOffset receivedAt, List<string> errors)
    {
        var time = ParseTime(timeValue, errors);
        return time is null
            ? null
            : new DateTimeOffset(receivedAt.Year, receivedAt.Month, receivedAt.Day, time.Value.Hour, time.Value.Minute, time.Value.Second, TimeSpan.Zero)
                .AddTicks(time.Value.Ticks % TimeSpan.TicksPerSecond);
    }

    private static DateTimeOffset? ParseDateTime(string timeValue, string dateValue, DateTimeOffset receivedAt, List<string> errors)
    {
        var time = ParseTime(timeValue, errors);
        if (time is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dateValue) || dateValue.Length != 6)
        {
            errors.Add("RMC date is missing or malformed.");
            return null;
        }

        if (!int.TryParse(dateValue[..2], out var day)
            || !int.TryParse(dateValue.Substring(2, 2), out var month)
            || !int.TryParse(dateValue.Substring(4, 2), out var year))
        {
            errors.Add("RMC date is malformed.");
            return null;
        }

        year += year >= 80 ? 1900 : 2000;
        try
        {
            return new DateTimeOffset(year, month, day, time.Value.Hour, time.Value.Minute, time.Value.Second, TimeSpan.Zero)
                .AddTicks(time.Value.Ticks % TimeSpan.TicksPerSecond);
        }
        catch (ArgumentOutOfRangeException)
        {
            errors.Add("RMC timestamp is out of range.");
            return null;
        }
    }

    private static TimeOnly? ParseTime(string timeValue, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(timeValue) || timeValue.Length < 6)
        {
            errors.Add("UTC time is missing or malformed.");
            return null;
        }

        if (!int.TryParse(timeValue[..2], out var hour)
            || !int.TryParse(timeValue.Substring(2, 2), out var minute)
            || !double.TryParse(timeValue[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out var secondsValue))
        {
            errors.Add("UTC time is malformed.");
            return null;
        }

        var second = (int)Math.Floor(secondsValue);
        var fractionalTicks = (long)((secondsValue - second) * TimeSpan.TicksPerSecond);
        try
        {
            return new TimeOnly(hour, minute, second).Add(TimeSpan.FromTicks(fractionalTicks));
        }
        catch (ArgumentOutOfRangeException)
        {
            errors.Add("UTC time is out of range.");
            return null;
        }
    }

    private static int? ParseNullableInt(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{label} is malformed.");
        return null;
    }

    private static double? ParseNullableDouble(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        errors.Add($"{label} is malformed.");
        return null;
    }

    private static bool IsChecksumValid(string payload, string providedChecksum)
    {
        if (providedChecksum.Length < 2)
        {
            return false;
        }

        var checksum = 0;
        foreach (var character in payload)
        {
            checksum ^= character;
        }

        return int.TryParse(providedChecksum[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var provided)
            && checksum == provided;
    }

    private static string GetField(string[] fields, int index)
    {
        return index < fields.Length ? fields[index] : string.Empty;
    }
}
