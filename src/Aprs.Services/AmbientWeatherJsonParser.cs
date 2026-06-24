using System.Text.Json;

namespace Aprs.Services;

public sealed class AmbientWeatherJsonParser
{
    public AmbientWeatherParseResult Parse(string rawJson, AmbientWeatherConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return AmbientWeatherParseResult.Failed("Ambient Weather response is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            var apiError = GetApiError(root);
            if (!string.IsNullOrWhiteSpace(apiError))
            {
                return AmbientWeatherParseResult.Failed(apiError);
            }

            if (!TryGetObservation(root, out var observationElement))
            {
                return AmbientWeatherParseResult.Failed("Ambient Weather response does not contain observation data.");
            }

            var diagnostics = BuildDiagnostics(observationElement, rawJson);
            var deviceId = FirstNonWhiteSpace(
                GetString(observationElement, "macAddress"),
                GetString(observationElement, "deviceId"),
                GetString(observationElement, "stationId"),
                configuration.DeviceId,
                configuration.SourceName);

            var observation = new CommonWeatherObservation(
                configuration.SourceName,
                WeatherObservationSourceType.AmbientWeather,
                deviceId,
                Callsign: null,
                ReadTimestamp(observationElement, receivedAtUtc),
                Latitude: null,
                Longitude: null,
                RoundToInt(ReadValue(observationElement, "winddir", "wind_direction")),
                WindSpeedMph: ReadValue(observationElement, "windspeedmph", "wind_speed_mph"),
                WindGustMph: ReadValue(observationElement, "windgustmph", "maxdailygust", "wind_gust_mph"),
                TemperatureFahrenheit: ReadValue(observationElement, "tempf", "outdoor_temperature_f"),
                RainLastHourInches: ReadValue(observationElement, "hourlyrainin", "rain_last_hour_in"),
                RainLast24HoursInches: ReadValue(observationElement, "dailyrainin", "rain_last_24hr_in"),
                RainSinceMidnightInches: ReadValue(observationElement, "dailyrainin", "eventrainin"),
                HumidityPercent: RoundToInt(ReadValue(observationElement, "humidity", "humidityout")),
                BarometricPressureMillibars: InchesMercuryToMillibars(ReadValue(observationElement, "baromrelin", "baromabsin"))
                    ?? ReadValue(observationElement, "baromrelmb", "baromabsmb"),
                LuminosityWattsPerSquareMeter: RoundToInt(ReadValue(observationElement, "solarradiation")),
                UvIndex: ReadValue(observationElement, "uv"),
                SnowInches: null,
                LightningCount: null,
                LightningDistanceMiles: null,
                diagnostics,
                rawJson,
                WeatherDataState.Current,
                ValidationErrors: [],
                ValidationWarnings: []);

            return new AmbientWeatherParseResult(true, observation, diagnostics, null);
        }
        catch (JsonException ex)
        {
            return AmbientWeatherParseResult.Failed($"Ambient Weather response is not valid JSON: {ex.Message}");
        }
    }

    private static bool TryGetObservation(JsonElement root, out JsonElement observationElement)
    {
        observationElement = default;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            observationElement = root[0];
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("lastData", out var lastData) && lastData.ValueKind == JsonValueKind.Object)
            {
                observationElement = lastData;
                return true;
            }

            observationElement = root;
            return true;
        }

        return false;
    }

    private static string? GetApiError(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
        {
            return error.ValueKind switch
            {
                JsonValueKind.String => error.GetString(),
                JsonValueKind.Object => FirstNonWhiteSpace(GetString(error, "message"), GetString(error, "error")),
                _ => "Ambient Weather API returned an error."
            };
        }

        if (root.ValueKind == JsonValueKind.Object && GetDouble(root, "code") is { } code and >= 400)
        {
            return FirstNonWhiteSpace(GetString(root, "message"), $"Ambient Weather API returned status {code:0}.");
        }

        return null;
    }

    private static DateTimeOffset ReadTimestamp(JsonElement observationElement, DateTimeOffset? fallback)
    {
        var epochMilliseconds = ReadValue(observationElement, "dateutc");
        if (epochMilliseconds is not null)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(epochMilliseconds.Value));
        }

        var date = FirstNonWhiteSpace(GetString(observationElement, "date"), GetString(observationElement, "lastRain"));
        if (DateTimeOffset.TryParse(date, out var parsed))
        {
            return parsed;
        }

        return fallback ?? DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, string> BuildDiagnostics(JsonElement observationElement, string rawJson)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = "ambient_weather_observation",
            ["raw_length"] = rawJson.Length.ToString()
        };

        AddIfPresent(diagnostics, "mac_address", GetString(observationElement, "macAddress"));
        AddIfPresent(diagnostics, "device_id", GetString(observationElement, "deviceId"));
        AddIfPresent(diagnostics, "weekly_rain_in", GetDouble(observationElement, "weeklyrainin")?.ToString());
        AddIfPresent(diagnostics, "monthly_rain_in", GetDouble(observationElement, "monthlyrainin")?.ToString());
        AddIfPresent(diagnostics, "yearly_rain_in", GetDouble(observationElement, "yearlyrainin")?.ToString());
        AddIfPresent(diagnostics, "battery", FirstNonWhiteSpace(GetString(observationElement, "battout"), GetString(observationElement, "batt1")));
        return diagnostics;
    }

    private static double? ReadValue(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetDouble(element, propertyName);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static double? GetDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
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
