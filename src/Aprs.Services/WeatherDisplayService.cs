using Aprs.Core;

namespace Aprs.Services;

public sealed class WeatherDisplayService : IWeatherDisplayService
{
    private readonly Dictionary<string, WeatherStationDisplayRecord> stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly WeatherDisplayConfiguration configuration;

    public WeatherDisplayService()
        : this(WeatherDisplayConfiguration.Default)
    {
    }

    public WeatherDisplayService(WeatherDisplayConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public WeatherStationDisplayRecord? AcceptWeatherPacket(WeatherAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        if (!packet.IsValid)
        {
            return null;
        }

        var stationId = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        if (string.IsNullOrWhiteSpace(stationId))
        {
            return null;
        }

        var existing = stations.GetValueOrDefault(stationId);
        var record = new WeatherStationDisplayRecord(
            stationId,
            stationId,
            WeatherStationSourceType.AprsWeatherStation,
            packet.Latitude ?? existing?.Latitude,
            packet.Longitude ?? existing?.Longitude,
            packet.WindDirectionDegrees,
            packet.WindSpeedMph,
            packet.WindGustMph,
            packet.TemperatureFahrenheit,
            packet.RainLastHourHundredthsInch,
            packet.RainLast24HoursHundredthsInch,
            packet.RainSinceMidnightHundredthsInch,
            packet.HumidityPercent,
            packet.BarometricPressureMillibars,
            packet.LuminosityWattsPerSquareMeter,
            UvIndex: null,
            packet.SnowHundredthsInch,
            LightningEventInformation: null,
            packet.ReceivedAtUtc,
            TimeSpan.Zero,
            WeatherDataState.Current,
            packet.RawLine,
            MapOrigin(packetSource));

        stations[stationId] = record;
        return record;
    }

    public WeatherStationDisplayRecord UpsertWeatherStation(WeatherStationDisplayRecord record)
    {
        var updated = record with
        {
            StationId = NormalizeStationId(record.StationId)
        };
        stations[updated.StationId] = updated;
        return updated;
    }

    public void UpdateStaleStates(DateTimeOffset now)
    {
        foreach (var (stationId, record) in stations.ToArray())
        {
            var age = now - record.LastUpdateUtc;
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            stations[stationId] = record with
            {
                DataAge = age,
                DataState = CalculateState(record.LastUpdateUtc, now)
            };
        }
    }

    public IReadOnlyCollection<WeatherStationDisplayRecord> GetAllWeatherStations()
    {
        return Sort(stations.Values);
    }

    public IReadOnlyCollection<WeatherStationDisplayRecord> GetCurrentWeatherStations()
    {
        return Sort(stations.Values.Where(station =>
            station.DataState == WeatherDataState.Current
            || (!configuration.ExcludeStaleFromCurrentList && station.DataState == WeatherDataState.Stale)));
    }

    public IReadOnlyCollection<WeatherStationDisplayRecord> GetStaleWeatherStations()
    {
        return Sort(stations.Values.Where(station => station.DataState is WeatherDataState.Stale or WeatherDataState.Expired));
    }

    public WeatherStationDisplayRecord? GetWeatherStation(string stationId)
    {
        return stations.TryGetValue(NormalizeStationId(stationId), out var station) ? station : null;
    }

    public void Clear()
    {
        stations.Clear();
    }

    private WeatherDataState CalculateState(DateTimeOffset lastUpdateUtc, DateTimeOffset now)
    {
        var age = now - lastUpdateUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age >= configuration.ExpiredThreshold)
        {
            return WeatherDataState.Expired;
        }

        return age > configuration.CurrentThreshold ? WeatherDataState.Stale : WeatherDataState.Current;
    }

    private static WeatherStationOrigin MapOrigin(AprsPacketSource packetSource)
    {
        return packetSource switch
        {
            AprsPacketSource.AprsIs => WeatherStationOrigin.AprsIs,
            AprsPacketSource.Rf or AprsPacketSource.TcpKiss or AprsPacketSource.SerialKiss or AprsPacketSource.Direwolf or AprsPacketSource.Agwpe => WeatherStationOrigin.Rf,
            AprsPacketSource.Replay => WeatherStationOrigin.Replay,
            AprsPacketSource.Simulation => WeatherStationOrigin.Simulation,
            _ => WeatherStationOrigin.Unknown
        };
    }

    private static string FormatSourceCallsign(string callsign, int? ssid)
    {
        return ssid is null ? callsign.Trim().ToUpperInvariant() : $"{callsign.Trim().ToUpperInvariant()}-{ssid}";
    }

    private static string NormalizeStationId(string stationId)
    {
        return stationId.Trim().ToUpperInvariant();
    }

    private static IReadOnlyCollection<WeatherStationDisplayRecord> Sort(IEnumerable<WeatherStationDisplayRecord> stationValues)
    {
        return stationValues
            .OrderBy(station => station.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(station => station.StationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
