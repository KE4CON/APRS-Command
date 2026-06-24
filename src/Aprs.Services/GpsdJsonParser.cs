using System.Globalization;
using System.Text.Json;

namespace Aprs.Services;

public sealed class GpsdJsonParser : IGpsdJsonParser
{
    private const double MetersPerSecondToKnots = 1.9438444924406;

    public GpsdParseResult Parse(string rawJson, string sourceName = "gpsd", DateTimeOffset? receivedAtUtc = null)
    {
        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return GpsdParseResult.Failed(rawJson, ["gpsd JSON report is empty."]);
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            if (!TryGetString(root, "class", out var reportClass))
            {
                return GpsdParseResult.Failed(rawJson, ["gpsd JSON report is missing class."]);
            }

            return reportClass.ToUpperInvariant() switch
            {
                "VERSION" => ParsedWithoutPosition(GpsdReportType.Version, rawJson),
                "WATCH" => ParsedWithoutPosition(GpsdReportType.Watch, rawJson),
                "TPV" => ParseTpv(root, rawJson, sourceName, receivedAt),
                "SKY" => ParseSky(root, rawJson),
                _ => new GpsdParseResult(
                    IsParsed: false,
                    GpsdReportType.Unknown,
                    Position: null,
                    SatelliteCount: null,
                    UsedSatelliteCount: null,
                    Hdop: null,
                    rawJson,
                    [$"Unsupported gpsd report class '{reportClass}'."],
                    Warnings: [])
            };
        }
        catch (JsonException)
        {
            return GpsdParseResult.Failed(rawJson, ["gpsd JSON report is malformed."]);
        }
    }

    private static GpsdParseResult ParseTpv(JsonElement root, string rawJson, string sourceName, DateTimeOffset receivedAt)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var mode = TryGetInt(root, "mode", out var parsedMode) ? parsedMode : (int?)null;
        var latitude = TryGetDouble(root, "lat", out var parsedLatitude) ? parsedLatitude : (double?)null;
        var longitude = TryGetDouble(root, "lon", out var parsedLongitude) ? parsedLongitude : (double?)null;
        var altitude = TryGetDouble(root, "alt", out var parsedAltitude) ? parsedAltitude : (double?)null;
        var speedKnots = TryGetDouble(root, "speed", out var parsedSpeed)
            ? parsedSpeed * MetersPerSecondToKnots
            : (double?)null;
        var course = TryGetDouble(root, "track", out var parsedTrack) ? parsedTrack : (double?)null;
        var timestamp = TryGetString(root, "time", out var time)
            && DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp)
                ? parsedTimestamp.ToUniversalTime()
                : (DateTimeOffset?)null;
        if (TryGetString(root, "time", out _) && timestamp is null)
        {
            warnings.Add("gpsd TPV time is malformed.");
        }

        var deviceSource = TryGetString(root, "device", out var device)
            ? $"{sourceName}:{device}"
            : sourceName;
        var fixValid = mode >= 2 && latitude is not null && longitude is not null;
        if (mode >= 2 && (latitude is null || longitude is null))
        {
            errors.Add("gpsd TPV has a valid mode but is missing coordinates.");
        }

        var position = new GpsPosition(
            latitude,
            longitude,
            altitude,
            speedKnots,
            course,
            timestamp,
            fixValid,
            FixQuality: mode,
            SatelliteCount: null,
            Hdop: null,
            deviceSource,
            rawJson,
            receivedAt);

        return new GpsdParseResult(
            IsParsed: errors.Count == 0,
            GpsdReportType.Tpv,
            errors.Count == 0 ? position : null,
            SatelliteCount: null,
            UsedSatelliteCount: null,
            Hdop: null,
            rawJson,
            errors,
            warnings);
    }

    private static GpsdParseResult ParseSky(JsonElement root, string rawJson)
    {
        var satelliteCount = 0;
        var usedSatelliteCount = 0;
        if (root.TryGetProperty("satellites", out var satellites) && satellites.ValueKind == JsonValueKind.Array)
        {
            foreach (var satellite in satellites.EnumerateArray())
            {
                satelliteCount++;
                if (satellite.TryGetProperty("used", out var used) && used.ValueKind == JsonValueKind.True)
                {
                    usedSatelliteCount++;
                }
            }
        }

        var hdop = TryGetDouble(root, "hdop", out var parsedHdop) ? parsedHdop : (double?)null;

        return new GpsdParseResult(
            IsParsed: true,
            GpsdReportType.Sky,
            Position: null,
            SatelliteCount: satelliteCount,
            UsedSatelliteCount: usedSatelliteCount,
            Hdop: hdop,
            rawJson,
            Errors: [],
            Warnings: []);
    }

    private static GpsdParseResult ParsedWithoutPosition(GpsdReportType reportType, string rawJson)
    {
        return new GpsdParseResult(
            IsParsed: true,
            reportType,
            Position: null,
            SatelliteCount: null,
            UsedSatelliteCount: null,
            Hdop: null,
            rawJson,
            Errors: [],
            Warnings: []);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDouble(JsonElement root, string propertyName, out double value)
    {
        if (root.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        if (root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
