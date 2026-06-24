using System.Text.Json;

namespace Aprs.Services;

public sealed class DavisWeatherJsonParser
{
    public DavisWeatherParseResult Parse(string rawJson, DavisWeatherConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return DavisWeatherParseResult.Failed("Davis WeatherLink response is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            var apiError = GetApiError(root);
            if (!string.IsNullOrWhiteSpace(apiError))
            {
                return DavisWeatherParseResult.Failed(apiError);
            }

            if (!TryGetObservation(root, out var observationElement, out var sensorElement))
            {
                return DavisWeatherParseResult.Failed("Davis WeatherLink response does not contain observation data.");
            }

            var stationId = FirstNonWhiteSpace(
                GetString(root, "station_id"),
                GetNumberAsString(root, "station_id"),
                configuration.StationId);
            var deviceId = FirstNonWhiteSpace(
                GetString(sensorElement, "lsid"),
                GetNumberAsString(sensorElement, "lsid"),
                stationId,
                configuration.SourceName);
            var diagnostics = BuildDiagnostics(root, sensorElement, rawJson);

            var observation = new CommonWeatherObservation(
                configuration.SourceName,
                WeatherObservationSourceType.DavisWeatherLink,
                deviceId,
                Callsign: null,
                ReadTimestamp(observationElement, receivedAtUtc),
                Latitude: null,
                Longitude: null,
                RoundToInt(ReadValue(observationElement, "wind_dir", "wind_direction")),
                WindSpeedMph: ReadValue(observationElement, "wind_speed", "wind_speed_avg_last_1_min", "wind_speed_avg_last_2_min"),
                WindGustMph: ReadValue(observationElement, "wind_gust", "wind_speed_hi_last_10_min", "wind_speed_hi_last_2_min"),
                TemperatureFahrenheit: ReadValue(observationElement, "temp", "temp_out", "outdoor_temperature"),
                RainLastHourInches: ReadValue(observationElement, "rainfall_last_60_min", "rainfall_last_1_hr", "rain_last_hour"),
                RainLast24HoursInches: ReadValue(observationElement, "rainfall_last_24_hr", "rain_last_24_hr"),
                RainSinceMidnightInches: ReadValue(observationElement, "rainfall_daily", "rain_storm", "rain_since_midnight"),
                HumidityPercent: RoundToInt(ReadValue(observationElement, "hum", "relative_humidity", "outdoor_humidity")),
                BarometricPressureMillibars: InchesMercuryToMillibars(ReadValue(observationElement, "bar", "bar_sea_level", "bar_absolute", "pressure_inhg"))
                    ?? ReadValue(observationElement, "pressure_mb", "barometer_mb"),
                LuminosityWattsPerSquareMeter: RoundToInt(ReadValue(observationElement, "solar_rad", "solar_radiation")),
                UvIndex: ReadValue(observationElement, "uv_index", "uv"),
                SnowInches: null,
                LightningCount: null,
                LightningDistanceMiles: null,
                diagnostics,
                rawJson,
                WeatherDataState.Current,
                ValidationErrors: [],
                ValidationWarnings: []);

            return new DavisWeatherParseResult(true, observation, diagnostics, null);
        }
        catch (JsonException ex)
        {
            return DavisWeatherParseResult.Failed($"Davis WeatherLink response is not valid JSON: {ex.Message}");
        }
    }

    private static bool TryGetObservation(JsonElement root, out JsonElement observationElement, out JsonElement sensorElement)
    {
        observationElement = default;
        sensorElement = default;

        if (root.TryGetProperty("sensors", out var sensors) && sensors.ValueKind == JsonValueKind.Array)
        {
            foreach (var sensor in sensors.EnumerateArray())
            {
                if (!sensor.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                {
                    continue;
                }

                sensorElement = sensor;
                observationElement = data[0];
                return true;
            }
        }

        if (root.TryGetProperty("data", out var rootData) && rootData.ValueKind == JsonValueKind.Array && rootData.GetArrayLength() > 0)
        {
            sensorElement = root;
            observationElement = rootData[0];
            return true;
        }

        if (root.TryGetProperty("observation", out var observation) && observation.ValueKind == JsonValueKind.Object)
        {
            sensorElement = root;
            observationElement = observation;
            return true;
        }

        return false;
    }

    private static string? GetApiError(JsonElement root)
    {
        if (GetDouble(root, "code") is { } code and >= 400)
        {
            return FirstNonWhiteSpace(GetString(root, "message"), $"Davis WeatherLink API returned status {code:0}.");
        }

        if (root.TryGetProperty("error", out var error))
        {
            return error.ValueKind switch
            {
                JsonValueKind.String => error.GetString(),
                JsonValueKind.Object => FirstNonWhiteSpace(GetString(error, "message"), GetString(error, "error")),
                _ => "Davis WeatherLink API returned an error."
            };
        }

        return null;
    }

    private static DateTimeOffset ReadTimestamp(JsonElement observationElement, DateTimeOffset? fallback)
    {
        var timestamp = ReadValue(observationElement, "ts", "timestamp", "time");
        return timestamp is null ? fallback ?? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(timestamp.Value));
    }

    private static Dictionary<string, string> BuildDiagnostics(JsonElement root, JsonElement sensorElement, string rawJson)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = "davis_weatherlink_observation",
            ["raw_length"] = rawJson.Length.ToString()
        };

        AddIfPresent(diagnostics, "station_id", FirstNonWhiteSpace(GetString(root, "station_id"), GetNumberAsString(root, "station_id")));
        AddIfPresent(diagnostics, "sensor_lsid", FirstNonWhiteSpace(GetString(sensorElement, "lsid"), GetNumberAsString(sensorElement, "lsid")));
        AddIfPresent(diagnostics, "sensor_type", FirstNonWhiteSpace(GetString(sensorElement, "sensor_type"), GetNumberAsString(sensorElement, "sensor_type")));
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
            _ => null
        };
    }

    private static string? GetNumberAsString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number ? property.ToString() : null;
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
