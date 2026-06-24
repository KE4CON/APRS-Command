using System.Text.Json;
using System.Web;

namespace Aprs.Services;

public sealed class EcowittWeatherPayloadParser
{
    public EcowittWeatherParseResult Parse(string payload, EcowittWeatherConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return EcowittWeatherParseResult.Failed("Ecowitt/Fine Offset payload is empty.");
        }

        var trimmed = payload.Trim();
        try
        {
            return trimmed.StartsWith('{') || trimmed.StartsWith('[')
                ? ParseJson(trimmed, configuration, receivedAtUtc)
                : ParseForm(trimmed, configuration, receivedAtUtc);
        }
        catch (JsonException ex)
        {
            return EcowittWeatherParseResult.Failed($"Ecowitt/Fine Offset payload is not valid JSON: {ex.Message}");
        }
    }

    private static EcowittWeatherParseResult ParseJson(string payload, EcowittWeatherConfiguration configuration, DateTimeOffset? receivedAtUtc)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return EcowittWeatherParseResult.Failed("Ecowitt/Fine Offset JSON payload does not contain an observation.");
        }

        var values = FlattenJson(root);
        return CreateObservation(values, payload, configuration, receivedAtUtc, "json");
    }

    private static EcowittWeatherParseResult ParseForm(string payload, EcowittWeatherConfiguration configuration, DateTimeOffset? receivedAtUtc)
    {
        var normalized = payload.TrimStart('?');
        var parsed = HttpUtility.ParseQueryString(normalized);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in parsed.AllKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            values[key!] = parsed[key!] ?? string.Empty;
        }

        if (values.Count == 0)
        {
            return EcowittWeatherParseResult.Failed(
                "Ecowitt/Fine Offset payload format is not recognized.",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["format"] = "unknown",
                    ["raw_length"] = payload.Length.ToString()
                });
        }

        return CreateObservation(values, payload, configuration, receivedAtUtc, "form");
    }

    private static EcowittWeatherParseResult CreateObservation(
        IReadOnlyDictionary<string, string> values,
        string rawPayload,
        EcowittWeatherConfiguration configuration,
        DateTimeOffset? receivedAtUtc,
        string format)
    {
        var diagnostics = BuildDiagnostics(values, rawPayload, format);
        var stationId = FirstNonWhiteSpace(
            FirstString(values, "stationtype", "model", "modelid"),
            FirstString(values, "mac", "macaddress", "deviceid", "stationid"),
            configuration.StationDeviceId,
            configuration.SourceName);

        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            WeatherObservationSourceType.EcowittFineOffsetGw1000,
            stationId,
            Callsign: null,
            ParseTimestamp(FirstString(values, "dateutc", "time", "timestamp"), receivedAtUtc ?? DateTimeOffset.UtcNow),
            Latitude: null,
            Longitude: null,
            RoundToInt(FirstDouble(values, "winddir", "wind_direction")),
            WindSpeedMph: FirstDouble(values, "windspeedmph", "wind_speed_mph"),
            WindGustMph: FirstDouble(values, "windgustmph", "maxdailygust", "wind_gust_mph"),
            TemperatureFahrenheit: FirstDouble(values, "tempf", "tempinf", "outdoortempf"),
            RainLastHourInches: FirstDouble(values, "hourlyrainin", "rainhourlyin", "rain_last_hour_in"),
            RainLast24HoursInches: FirstDouble(values, "dailyrainin", "rain24hin", "rain_last_24hr_in"),
            RainSinceMidnightInches: FirstDouble(values, "dailyrainin", "eventrainin", "raindayin"),
            HumidityPercent: RoundToInt(FirstDouble(values, "humidity", "humidityout", "outhumidity")),
            BarometricPressureMillibars: InchesMercuryToMillibars(FirstDouble(values, "baromrelin", "baromabsin", "baromin"))
                ?? FirstDouble(values, "baromrelmb", "baromabsmb", "pressuremb"),
            LuminosityWattsPerSquareMeter: RoundToInt(FirstDouble(values, "solarradiation", "solarradiationwpm2")),
            UvIndex: FirstDouble(values, "uv", "uvindex"),
            SnowInches: null,
            LightningCount: RoundToInt(FirstDouble(values, "lightning_num", "lightningcount")),
            LightningDistanceMiles: KilometersToMiles(FirstDouble(values, "lightning", "lightning_distance", "lightningdistkm")),
            diagnostics,
            rawPayload,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new EcowittWeatherParseResult(true, observation, diagnostics, null);
    }

    private static Dictionary<string, string> FlattenJson(JsonElement root)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            AddJsonValue(values, property.Name, property.Value);
        }

        return values;
    }

    private static void AddJsonValue(IDictionary<string, string> values, string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var nested in value.EnumerateObject())
            {
                AddJsonValue(values, nested.Name, nested.Value);
            }

            return;
        }

        if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            values[name] = value.ToString();
        }
    }

    private static Dictionary<string, string> BuildDiagnostics(IReadOnlyDictionary<string, string> values, string rawPayload, string format)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = "ecowitt_fine_offset_observation",
            ["format"] = format,
            ["raw_length"] = rawPayload.Length.ToString()
        };

        AddIfPresent(diagnostics, "station_type", FirstString(values, "stationtype", "model", "modelid"));
        AddIfPresent(diagnostics, "mac", FirstString(values, "mac", "macaddress"));
        AddIfPresent(diagnostics, "weekly_rain_in", FirstDouble(values, "weeklyrainin")?.ToString());
        AddIfPresent(diagnostics, "monthly_rain_in", FirstDouble(values, "monthlyrainin")?.ToString());
        AddIfPresent(diagnostics, "yearly_rain_in", FirstDouble(values, "yearlyrainin")?.ToString());
        AddIfPresent(diagnostics, "battery", FirstString(values, "wh65batt", "battout", "battery", "lowbatt"));

        foreach (var (key, value) in values.Where(pair => pair.Key.StartsWith("temp", StringComparison.OrdinalIgnoreCase)
            || pair.Key.StartsWith("humidity", StringComparison.OrdinalIgnoreCase)
            || pair.Key.StartsWith("soil", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics[$"extra_{key}"] = value;
        }

        return diagnostics;
    }

    private static DateTimeOffset ParseTimestamp(string? value, DateTimeOffset fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (long.TryParse(value, out var numeric))
        {
            return numeric > 9_999_999_999
                ? DateTimeOffset.FromUnixTimeMilliseconds(numeric)
                : DateTimeOffset.FromUnixTimeSeconds(numeric);
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string? FirstString(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static double? FirstDouble(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && double.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static void AddIfPresent(IDictionary<string, string> diagnostics, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            diagnostics[key] = value;
        }
    }

    private static int? RoundToInt(double? value)
    {
        return value is null ? null : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private static double? InchesMercuryToMillibars(double? inchesMercury)
    {
        return inchesMercury is null ? null : inchesMercury.Value * 33.8638866667;
    }

    private static double? KilometersToMiles(double? kilometers)
    {
        return kilometers is null ? null : kilometers.Value * 0.6213711922;
    }
}
