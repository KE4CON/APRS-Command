using System.Text.Json;

namespace Aprs.Services;

public sealed class WeatherSoftwareImportParser
{
    public WeatherSoftwareImportParseResult Parse(
        string payload,
        WeatherSoftwareImportConfiguration configuration,
        DateTimeOffset? receivedAtUtc = null,
        DateTimeOffset? fileLastWriteUtc = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return WeatherSoftwareImportParseResult.Failed("Weather software payload is empty.");
        }

        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        try
        {
            var values = configuration.SoftwareType switch
            {
                WeatherSoftwareType.GenericJson => ParseJson(payload),
                WeatherSoftwareType.GenericCsv => ParseCsv(payload, configuration.Delimiter),
                WeatherSoftwareType.GenericKeyValueText => ParseKeyValue(payload, configuration.Delimiter),
                WeatherSoftwareType.CumulusMx
                    or WeatherSoftwareType.GenericRealtimeTxt
                    or WeatherSoftwareType.WeatherDisplay
                    or WeatherSoftwareType.WeeWx => ParseRealtime(payload),
                WeatherSoftwareType.LocalHttpEndpoint => LooksLikeJson(payload) ? ParseJson(payload) : ParseKeyValue(payload, configuration.Delimiter),
                _ => LooksLikeJson(payload) ? ParseJson(payload) : ParseKeyValue(payload, configuration.Delimiter)
            };

            if (values.Count == 0)
            {
                return WeatherSoftwareImportParseResult.Failed(
                    "Weather software payload format is not recognized.",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["format"] = "unknown",
                        ["raw_length"] = payload.Length.ToString()
                    });
            }

            return CreateObservation(values, payload, configuration, receivedAt, fileLastWriteUtc);
        }
        catch (JsonException ex)
        {
            return WeatherSoftwareImportParseResult.Failed($"Weather software JSON payload is malformed: {ex.Message}");
        }
    }

    public IReadOnlyList<WeatherSoftwareFieldMapping> GetDefaultMappingPlaceholders()
    {
        return
        [
            new WeatherSoftwareFieldMapping("winddir", "WindDirectionDegrees", "degrees", "Future UI can let users override source field names."),
            new WeatherSoftwareFieldMapping("windspeedmph", "WindSpeedMph", "mph", "Future UI can select units per field."),
            new WeatherSoftwareFieldMapping("tempf", "TemperatureFahrenheit", "F", "Future UI can map CSV columns or JSON paths."),
            new WeatherSoftwareFieldMapping("humidity", "HumidityPercent", "%", "Future UI can map key-value aliases.")
        ];
    }

    private static WeatherSoftwareImportParseResult CreateObservation(
        IReadOnlyDictionary<string, string> values,
        string payload,
        WeatherSoftwareImportConfiguration configuration,
        DateTimeOffset receivedAt,
        DateTimeOffset? fileLastWriteUtc)
    {
        var diagnostics = BuildDiagnostics(values, payload, configuration, fileLastWriteUtc);
        var timestamp = ParseTimestamp(FirstString(values, "timestamp", "dateutc", "datetime", "date_time", "time"), fileLastWriteUtc ?? receivedAt);
        var sourceType = MapSourceType(configuration.SoftwareType);
        var stationId = FirstNonWhiteSpace(
            FirstString(values, "station", "stationid", "station_id", "source", "software"),
            configuration.SourceName);

        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            sourceType,
            stationId,
            Callsign: null,
            timestamp,
            Latitude: FirstDouble(values, "latitude", "lat"),
            Longitude: FirstDouble(values, "longitude", "lon", "lng"),
            RoundToInt(FirstDouble(values, "winddir", "wind_dir", "bearing", "windbearing")),
            WindSpeedMph: FirstDouble(values, "windspeedmph", "wind_speed_mph", "ws", "wind"),
            WindGustMph: FirstDouble(values, "windgustmph", "wind_gust_mph", "gust", "wgust"),
            TemperatureFahrenheit: FirstDouble(values, "tempf", "temperaturef", "outtempf", "temp", "temperature"),
            RainLastHourInches: FirstDouble(values, "rainlasthourin", "hourlyrainin", "rainhour", "rain1h"),
            RainLast24HoursInches: FirstDouble(values, "rain24hin", "rainlast24hourin", "rain24h"),
            RainSinceMidnightInches: FirstDouble(values, "dailyrainin", "raindayin", "rainmidnight", "rain"),
            HumidityPercent: RoundToInt(FirstDouble(values, "humidity", "hum", "outhumidity")),
            BarometricPressureMillibars: InchesMercuryToMillibars(FirstDouble(values, "baromrelin", "baromin", "pressurein"))
                ?? FirstDouble(values, "pressuremb", "barommb", "barometer", "pressure"),
            LuminosityWattsPerSquareMeter: RoundToInt(FirstDouble(values, "solarradiation", "solar", "luminosity")),
            UvIndex: FirstDouble(values, "uv", "uvindex"),
            SnowInches: FirstDouble(values, "snowin", "snow"),
            LightningCount: RoundToInt(FirstDouble(values, "lightningcount", "lightning_num")),
            LightningDistanceMiles: FirstDouble(values, "lightningdistancemi", "lightning_distance_mi"),
            diagnostics,
            payload,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new WeatherSoftwareImportParseResult(true, observation, diagnostics, null);
    }

    private static Dictionary<string, string> ParseRealtime(string payload)
    {
        var tokens = payload.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 11)
        {
            return [];
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "realtime.txt",
            ["date_time"] = $"{tokens[0]} {tokens[1]}",
            ["tempf"] = tokens[2],
            ["humidity"] = tokens[3],
            ["windspeedmph"] = tokens[5],
            ["windgustmph"] = tokens[6],
            ["winddir"] = tokens[7],
            ["rainlasthourin"] = tokens[8],
            ["dailyrainin"] = tokens[9],
            ["baromrelin"] = tokens[10],
            ["software"] = "realtime.txt"
        };
    }

    private static Dictionary<string, string> ParseJson(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "json"
        };
        FlattenJson(root, values);
        return values;
    }

    private static Dictionary<string, string> ParseCsv(string payload, string delimiter)
    {
        var lines = payload.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return [];
        }

        var separator = string.IsNullOrEmpty(delimiter) ? "," : delimiter;
        var headers = lines[0].Split(separator, StringSplitOptions.TrimEntries);
        var values = lines[1].Split(separator, StringSplitOptions.TrimEntries);
        if (headers.Length == 0 || values.Length == 0)
        {
            return [];
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "csv"
        };
        for (var i = 0; i < Math.Min(headers.Length, values.Length); i++)
        {
            result[NormalizeKey(headers[i])] = values[i];
        }

        return result;
    }

    private static Dictionary<string, string> ParseKeyValue(string payload, string delimiter)
    {
        var separators = new[] { delimiter, ",", ";", "&", "\r", "\n" }
            .Where(separator => !string.IsNullOrEmpty(separator))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var tokens = payload.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "key-value"
        };

        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                continue;
            }

            values[NormalizeKey(token[..separator])] = token[(separator + 1)..].Trim();
        }

        return values.Count == 1 ? [] : values;
    }

    private static void FlattenJson(JsonElement element, IDictionary<string, string> values)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenJson(property.Value, values);
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                values[NormalizeKey(property.Name)] = property.Value.ToString();
            }
        }
    }

    private static Dictionary<string, string> BuildDiagnostics(
        IReadOnlyDictionary<string, string> values,
        string payload,
        WeatherSoftwareImportConfiguration configuration,
        DateTimeOffset? fileLastWriteUtc)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = "weather_software_import_observation",
            ["software_type"] = configuration.SoftwareType.ToString(),
            ["format"] = FirstString(values, "format") ?? "unknown",
            ["raw_length"] = payload.Length.ToString()
        };

        if (fileLastWriteUtc is not null)
        {
            diagnostics["file_last_write_utc"] = fileLastWriteUtc.Value.ToString("O");
        }

        AddIfPresent(diagnostics, "battery", FirstString(values, "battery", "batt", "status"));
        AddIfPresent(diagnostics, "rain_rate", FirstString(values, "rainrate", "rrate", "rain_rate"));
        return diagnostics;
    }

    private static WeatherObservationSourceType MapSourceType(WeatherSoftwareType softwareType)
    {
        return softwareType switch
        {
            WeatherSoftwareType.CumulusMx => WeatherObservationSourceType.CumulusMx,
            WeatherSoftwareType.WeeWx => WeatherObservationSourceType.WeeWx,
            WeatherSoftwareType.WeatherDisplay => WeatherObservationSourceType.WeatherDisplay,
            _ => WeatherObservationSourceType.FileImport
        };
    }

    private static bool LooksLikeJson(string payload)
    {
        var trimmed = payload.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
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
        foreach (var key in keys.Select(NormalizeKey))
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
        foreach (var key in keys.Select(NormalizeKey))
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
}
