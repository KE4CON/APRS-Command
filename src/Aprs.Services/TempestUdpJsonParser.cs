using System.Text.Json;

namespace Aprs.Services;

public sealed class TempestUdpJsonParser
{
    public TempestUdpParseResult Parse(string rawJson, TempestUdpConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return TempestUdpParseResult.Failed("Tempest UDP payload is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            var messageType = GetString(root, "type");
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return TempestUdpParseResult.Failed("Tempest UDP payload is missing message type.");
            }

            return messageType switch
            {
                "obs_st" => ParseObsSt(root, rawJson, configuration, receivedAtUtc),
                "rapid_wind" => ParseRapidWind(root, rawJson, configuration, receivedAtUtc),
                "evt_precip" or "evt_strike" or "device_status" or "hub_status" => ParseDiagnosticsOnly(root, messageType, rawJson),
                _ => TempestUdpParseResult.Ignored(messageType, BuildCommonDiagnostics(root, messageType, rawJson))
            };
        }
        catch (JsonException ex)
        {
            return TempestUdpParseResult.Failed($"Tempest UDP payload is not valid JSON: {ex.Message}");
        }
    }

    private static TempestUdpParseResult ParseObsSt(
        JsonElement root,
        string rawJson,
        TempestUdpConfiguration configuration,
        DateTimeOffset? receivedAtUtc)
    {
        if (!TryGetArray(root, "obs", out var obsArray) || obsArray.GetArrayLength() == 0)
        {
            return TempestUdpParseResult.Failed("Tempest obs_st payload is missing obs data.");
        }

        var values = obsArray[0];
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() < 9)
        {
            return TempestUdpParseResult.Failed("Tempest obs_st data is incomplete.");
        }

        var timestamp = FromUnixSeconds(GetDouble(values, 0), receivedAtUtc);
        var windAverageMetersPerSecond = GetDouble(values, 2);
        var windGustMetersPerSecond = GetDouble(values, 3);
        var windDirection = RoundToInt(GetDouble(values, 4));
        var pressureMillibars = GetDouble(values, 6);
        var temperatureCelsius = GetDouble(values, 7);
        var humidity = RoundToInt(GetDouble(values, 8));
        var illuminanceLux = GetDouble(values, 9);
        var uvIndex = GetDouble(values, 10);
        var solarRadiation = GetDouble(values, 11);
        var rainAccumulatedMillimeters = GetDouble(values, 12);
        var lightningDistanceKilometers = GetDouble(values, 14);
        var lightningStrikeCount = RoundToInt(GetDouble(values, 15));
        var diagnostics = BuildCommonDiagnostics(root, "obs_st", rawJson);

        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            WeatherObservationSourceType.WeatherFlowTempest,
            GetString(root, "serial_number"),
            Callsign: null,
            timestamp,
            Latitude: null,
            Longitude: null,
            windDirection,
            WindSpeedMph: MetersPerSecondToMph(windAverageMetersPerSecond),
            WindGustMph: MetersPerSecondToMph(windGustMetersPerSecond),
            TemperatureFahrenheit: CelsiusToFahrenheit(temperatureCelsius),
            RainLastHourInches: MillimetersToInches(rainAccumulatedMillimeters),
            RainLast24HoursInches: null,
            RainSinceMidnightInches: null,
            humidity,
            pressureMillibars,
            LuminosityWattsPerSquareMeter: RoundToInt(solarRadiation ?? illuminanceLux),
            uvIndex,
            SnowInches: null,
            lightningStrikeCount,
            LightningDistanceMiles: KilometersToMiles(lightningDistanceKilometers),
            diagnostics,
            rawJson,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new TempestUdpParseResult(true, "obs_st", observation, diagnostics, null);
    }

    private static TempestUdpParseResult ParseRapidWind(
        JsonElement root,
        string rawJson,
        TempestUdpConfiguration configuration,
        DateTimeOffset? receivedAtUtc)
    {
        if (!TryGetArray(root, "ob", out var values) || values.GetArrayLength() < 3)
        {
            return TempestUdpParseResult.Failed("Tempest rapid_wind payload is missing wind data.");
        }

        var timestamp = FromUnixSeconds(GetDouble(values, 0), receivedAtUtc);
        var diagnostics = BuildCommonDiagnostics(root, "rapid_wind", rawJson);
        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            WeatherObservationSourceType.WeatherFlowTempest,
            GetString(root, "serial_number"),
            Callsign: null,
            timestamp,
            Latitude: null,
            Longitude: null,
            RoundToInt(GetDouble(values, 2)),
            WindSpeedMph: MetersPerSecondToMph(GetDouble(values, 1)),
            WindGustMph: null,
            TemperatureFahrenheit: null,
            RainLastHourInches: null,
            RainLast24HoursInches: null,
            RainSinceMidnightInches: null,
            HumidityPercent: null,
            BarometricPressureMillibars: null,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            diagnostics,
            rawJson,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new TempestUdpParseResult(true, "rapid_wind", observation, diagnostics, null);
    }

    private static TempestUdpParseResult ParseDiagnosticsOnly(JsonElement root, string messageType, string rawJson)
    {
        return TempestUdpParseResult.Ignored(messageType, BuildCommonDiagnostics(root, messageType, rawJson));
    }

    private static Dictionary<string, string> BuildCommonDiagnostics(JsonElement root, string messageType, string rawJson)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_type"] = messageType,
            ["raw_length"] = rawJson.Length.ToString()
        };

        AddIfPresent(diagnostics, "serial_number", GetString(root, "serial_number"));
        AddIfPresent(diagnostics, "hub_sn", GetString(root, "hub_sn"));
        AddIfPresent(diagnostics, "firmware_revision", GetString(root, "firmware_revision"));
        AddIfPresent(diagnostics, "uptime", GetDouble(root, "uptime")?.ToString());
        return diagnostics;
    }

    private static void AddIfPresent(IDictionary<string, string> diagnostics, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            diagnostics[key] = value;
        }
    }

    private static bool TryGetArray(JsonElement root, string propertyName, out JsonElement element)
    {
        return root.TryGetProperty(propertyName, out element) && element.ValueKind == JsonValueKind.Array;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
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

    private static double? GetDouble(JsonElement array, int index)
    {
        if (array.ValueKind != JsonValueKind.Array || index >= array.GetArrayLength())
        {
            return null;
        }

        var value = array[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed) ? parsed : null;
    }

    private static DateTimeOffset FromUnixSeconds(double? seconds, DateTimeOffset? fallback)
    {
        if (seconds is null)
        {
            return fallback ?? DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(seconds.Value, MidpointRounding.AwayFromZero));
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
