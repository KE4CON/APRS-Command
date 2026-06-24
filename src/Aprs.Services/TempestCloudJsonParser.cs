using System.Text.Json;

namespace Aprs.Services;

public sealed class TempestCloudJsonParser
{
    public TempestCloudParseResult Parse(string rawJson, TempestCloudConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return TempestCloudParseResult.Failed("Tempest cloud response is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            var apiError = GetApiError(root);
            if (!string.IsNullOrWhiteSpace(apiError))
            {
                return TempestCloudParseResult.Failed(apiError);
            }

            if (!TryGetObservationElement(root, out var observationElement))
            {
                return TempestCloudParseResult.Failed("Tempest cloud response does not contain an observation.");
            }

            var diagnostics = BuildDiagnostics(root, rawJson);
            var timestamp = ReadTimestamp(observationElement, receivedAtUtc);
            var stationId = FirstNonWhiteSpace(
                GetString(root, "station_id"),
                GetNumberAsString(root, "station_id"),
                configuration.StationId);
            var deviceId = FirstNonWhiteSpace(
                GetString(root, "device_id"),
                GetNumberAsString(root, "device_id"),
                configuration.DeviceId);
            var stationDeviceId = FirstNonWhiteSpace(deviceId, stationId, configuration.SourceName);

            var observation = new CommonWeatherObservation(
                configuration.SourceName,
                WeatherObservationSourceType.WeatherFlowTempest,
                stationDeviceId,
                Callsign: null,
                timestamp,
                Latitude: null,
                Longitude: null,
                WindDirectionDegrees: RoundToInt(ReadValue(observationElement, "wind_direction", 4)),
                WindSpeedMph: MetersPerSecondToMph(ReadValue(observationElement, "wind_avg", 2)),
                WindGustMph: MetersPerSecondToMph(ReadValue(observationElement, "wind_gust", 3)),
                TemperatureFahrenheit: CelsiusToFahrenheit(ReadValue(observationElement, "air_temperature", 7)),
                RainLastHourInches: MillimetersToInches(FirstValue(
                    ReadValue(observationElement, "precip_accum_last_1hr"),
                    ReadValue(observationElement, "precip_accum_local_day", 12))),
                RainLast24HoursInches: MillimetersToInches(ReadValue(observationElement, "precip_accum_local_day")),
                RainSinceMidnightInches: MillimetersToInches(ReadValue(observationElement, "precip_accum_local_day")),
                HumidityPercent: RoundToInt(ReadValue(observationElement, "relative_humidity", 8)),
                BarometricPressureMillibars: FirstValue(
                    ReadValue(observationElement, "barometric_pressure", 6),
                    ReadValue(observationElement, "sea_level_pressure"),
                    ReadValue(observationElement, "station_pressure")),
                LuminosityWattsPerSquareMeter: RoundToInt(FirstValue(
                    ReadValue(observationElement, "solar_radiation", 11),
                    ReadValue(observationElement, "illuminance", 9))),
                UvIndex: ReadValue(observationElement, "uv", 10),
                SnowInches: null,
                LightningCount: RoundToInt(ReadValue(observationElement, "lightning_strike_count", 15)),
                LightningDistanceMiles: KilometersToMiles(ReadValue(observationElement, "lightning_strike_last_distance", 14)),
                diagnostics,
                rawJson,
                WeatherDataState.Current,
                ValidationErrors: [],
                ValidationWarnings: []);

            return new TempestCloudParseResult(true, observation, diagnostics, null);
        }
        catch (JsonException ex)
        {
            return TempestCloudParseResult.Failed($"Tempest cloud response is not valid JSON: {ex.Message}");
        }
    }

    private static string? GetApiError(JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var code = GetDouble(status, "status_code");
        if (code is null or 0)
        {
            return null;
        }

        return FirstNonWhiteSpace(GetString(status, "status_message"), $"Tempest cloud API returned status {code:0}.");
    }

    private static bool TryGetObservationElement(JsonElement root, out JsonElement observationElement)
    {
        observationElement = default;
        if (!root.TryGetProperty("obs", out var obs))
        {
            return false;
        }

        if (obs.ValueKind == JsonValueKind.Object)
        {
            observationElement = obs;
            return true;
        }

        if (obs.ValueKind == JsonValueKind.Array && obs.GetArrayLength() > 0)
        {
            observationElement = obs[0];
            return true;
        }

        return false;
    }

    private static DateTimeOffset ReadTimestamp(JsonElement observationElement, DateTimeOffset? fallback)
    {
        return FromUnixSeconds(ReadValue(observationElement, "timestamp", 0), fallback);
    }

    private static Dictionary<string, string> BuildDiagnostics(JsonElement root, string rawJson)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = "tempest_cloud_observation",
            ["raw_length"] = rawJson.Length.ToString()
        };

        AddIfPresent(diagnostics, "station_id", FirstNonWhiteSpace(GetString(root, "station_id"), GetNumberAsString(root, "station_id")));
        AddIfPresent(diagnostics, "device_id", FirstNonWhiteSpace(GetString(root, "device_id"), GetNumberAsString(root, "device_id")));
        return diagnostics;
    }

    private static void AddIfPresent(IDictionary<string, string> diagnostics, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            diagnostics[key] = value;
        }
    }

    private static double? ReadValue(JsonElement observationElement, string propertyName, int? arrayIndex = null)
    {
        if (observationElement.ValueKind == JsonValueKind.Object)
        {
            return GetDouble(observationElement, propertyName);
        }

        if (observationElement.ValueKind == JsonValueKind.Array && arrayIndex is not null && arrayIndex.Value < observationElement.GetArrayLength())
        {
            var value = observationElement[arrayIndex.Value];
            return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed) ? parsed : null;
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

    private static DateTimeOffset FromUnixSeconds(double? seconds, DateTimeOffset? fallback)
    {
        if (seconds is null)
        {
            return fallback ?? DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(seconds.Value, MidpointRounding.AwayFromZero));
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static double? FirstValue(params double?[] values)
    {
        return values.FirstOrDefault(value => value is not null);
    }

    private static int? RoundToInt(double? value)
    {
        return value is null ? null : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private static double? MetersPerSecondToMph(double? metersPerSecond)
    {
        return metersPerSecond is null ? null : metersPerSecond.Value * 2.2369362921;
    }

    private static double? CelsiusToFahrenheit(double? celsius)
    {
        return celsius is null ? null : celsius.Value * 9 / 5 + 32;
    }

    private static double? MillimetersToInches(double? millimeters)
    {
        return millimeters is null ? null : millimeters.Value / 25.4;
    }

    private static double? KilometersToMiles(double? kilometers)
    {
        return kilometers is null ? null : kilometers.Value * 0.6213711922;
    }
}
