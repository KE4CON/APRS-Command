using Aprs.Core;

namespace Aprs.Services;

public sealed class PeetBrosWeatherParser
{
    private readonly AprsParser aprsParser = new();

    public PeetBrosParseResult Parse(string payload, PeetBrosConfiguration configuration, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return PeetBrosParseResult.Failed("Peet Bros payload is empty.");
        }

        var receivedAt = receivedAtUtc ?? DateTimeOffset.UtcNow;
        var trimmed = payload.Trim();
        if (LooksLikeAprsWeather(trimmed))
        {
            return ParseAprsWeather(trimmed, configuration, receivedAt);
        }

        return ParseKeyValueWeather(trimmed, configuration, receivedAt);
    }

    private PeetBrosParseResult ParseAprsWeather(string payload, PeetBrosConfiguration configuration, DateTimeOffset receivedAt)
    {
        var rawLine = payload.Contains('>', StringComparison.Ordinal)
            ? payload
            : $"PEETWX>APRS:{payload}";

        var packet = aprsParser.Parse(rawLine, receivedAt);
        if (packet is not WeatherAprsPacket weather || !weather.IsValid)
        {
            return PeetBrosParseResult.Failed(
                "Peet Bros APRS-style weather payload could not be parsed.",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["format"] = "aprs-weather",
                    ["raw_length"] = payload.Length.ToString()
                });
        }

        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "aprs-weather",
            ["raw_length"] = payload.Length.ToString()
        };
        AddIfPresent(diagnostics, "model", configuration.ModelName);

        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            WeatherObservationSourceType.PeetBrosUltimeter,
            configuration.ModelName ?? "Peet Bros ULTIMETER",
            Callsign: null,
            receivedAt,
            weather.Latitude,
            weather.Longitude,
            weather.WindDirectionDegrees,
            WindSpeedMph: weather.WindSpeedMph,
            WindGustMph: weather.WindGustMph,
            TemperatureFahrenheit: weather.TemperatureFahrenheit,
            RainLastHourInches: HundredthsToInches(weather.RainLastHourHundredthsInch),
            RainLast24HoursInches: HundredthsToInches(weather.RainLast24HoursHundredthsInch),
            RainSinceMidnightInches: HundredthsToInches(weather.RainSinceMidnightHundredthsInch),
            weather.HumidityPercent,
            weather.BarometricPressureMillibars,
            weather.LuminosityWattsPerSquareMeter,
            UvIndex: null,
            SnowInches: HundredthsToInches(weather.SnowHundredthsInch),
            LightningCount: null,
            LightningDistanceMiles: null,
            diagnostics,
            payload,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new PeetBrosParseResult(true, observation, diagnostics, null);
    }

    private static PeetBrosParseResult ParseKeyValueWeather(string payload, PeetBrosConfiguration configuration, DateTimeOffset receivedAt)
    {
        var values = ParsePairs(payload);
        if (values.Count == 0)
        {
            return PeetBrosParseResult.Failed(
                "Peet Bros payload format is not recognized. TODO: add exact ULTIMETER native packet variants as they are confirmed.",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["format"] = "unknown",
                    ["raw_length"] = payload.Length.ToString()
                });
        }

        var diagnostics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["format"] = "key-value",
            ["raw_length"] = payload.Length.ToString()
        };
        AddIfPresent(diagnostics, "model", FirstString(values, "MODEL", "M"));

        var observation = new CommonWeatherObservation(
            configuration.SourceName,
            WeatherObservationSourceType.PeetBrosUltimeter,
            FirstString(values, "DEVICE", "STATION", "MODEL") ?? configuration.ModelName ?? "Peet Bros ULTIMETER",
            Callsign: null,
            ParseTimestamp(FirstString(values, "TS", "TIMESTAMP", "TIME"), receivedAt),
            Latitude: null,
            Longitude: null,
            RoundToInt(FirstDouble(values, "WD", "WINDDIR", "DIR")),
            WindSpeedMph: FirstDouble(values, "WS", "WIND", "WINDSPD"),
            WindGustMph: FirstDouble(values, "WG", "GUST", "WINDGUST"),
            TemperatureFahrenheit: FirstDouble(values, "T", "TEMP", "TEMPF"),
            RainLastHourInches: FirstDouble(values, "R1", "RAIN1H", "RAINHOUR"),
            RainLast24HoursInches: FirstDouble(values, "R24", "RAIN24H", "RAINDAY"),
            RainSinceMidnightInches: FirstDouble(values, "RM", "RAINMIDNIGHT", "RAIN"),
            HumidityPercent: RoundToInt(FirstDouble(values, "H", "HUM", "HUMIDITY")),
            BarometricPressureMillibars: FirstDouble(values, "B", "BARO", "PRESSURE", "MB"),
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            diagnostics,
            payload,
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        return new PeetBrosParseResult(true, observation, diagnostics, null);
    }

    private static bool LooksLikeAprsWeather(string payload)
    {
        return payload.StartsWith("_", StringComparison.Ordinal)
            || payload.StartsWith("!", StringComparison.Ordinal)
            || payload.StartsWith("@", StringComparison.Ordinal)
            || payload.StartsWith("/", StringComparison.Ordinal)
            || payload.Contains(">APRS:", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParsePairs(string payload)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in payload.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                continue;
            }

            values[token[..separator].Trim()] = token[(separator + 1)..].Trim();
        }

        return values;
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

    private static DateTimeOffset ParseTimestamp(string? value, DateTimeOffset fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (long.TryParse(value, out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double? HundredthsToInches(int? hundredths)
    {
        return hundredths is null ? null : hundredths.Value / 100.0;
    }
}
