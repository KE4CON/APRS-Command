using Aprs.Core;

namespace Aprs.Services;

public sealed class StationDatabase : IStationDatabase
{
    private readonly Dictionary<string, StationSnapshot> stations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<StationTrailPoint>> trails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TacticalLabel> tacticalLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly StationAgingConfiguration agingConfiguration;
    private readonly StationTrailConfiguration trailConfiguration;

    public StationDatabase()
        : this(StationAgingConfiguration.Default, StationTrailConfiguration.Default)
    {
    }

    public StationDatabase(StationAgingConfiguration agingConfiguration)
        : this(agingConfiguration, StationTrailConfiguration.Default)
    {
    }

    public StationDatabase(StationTrailConfiguration trailConfiguration)
        : this(StationAgingConfiguration.Default, trailConfiguration)
    {
    }

    public StationDatabase(StationAgingConfiguration agingConfiguration, StationTrailConfiguration trailConfiguration)
    {
        this.agingConfiguration = agingConfiguration;
        this.trailConfiguration = trailConfiguration;
    }

    public void ProcessPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        if (!packet.IsValid)
        {
            return;
        }

        var stationKey = GetStationKey(packet);
        if (string.IsNullOrWhiteSpace(stationKey))
        {
            return;
        }

        var existing = stations.GetValueOrDefault(stationKey);
        var updated = CreateBaseUpdate(stationKey, packet, packetSource, existing);
        updated = ApplyPacketSpecificFields(updated, packet);

        stations[stationKey] = updated;
        AddTrailPointIfNeeded(stationKey, packet, packetSource);
    }

    public IReadOnlyCollection<StationSnapshot> GetAllStations()
    {
        return SortStations(stations.Values);
    }

    public IReadOnlyCollection<StationSnapshot> GetVisibleStations()
    {
        return SortStations(stations.Values.Where(IsVisible));
    }

    public IReadOnlyCollection<StationSnapshot> GetActiveStations()
    {
        return SortStations(stations.Values.Where(station => station.LifecycleState == StationLifecycleState.Active));
    }

    public IReadOnlyList<StationTrailPoint> GetTrail(string callsign)
    {
        return trails.TryGetValue(NormalizeStationKey(callsign), out var trail)
            ? trail.ToArray()
            : [];
    }

    public TacticalLabel SetTacticalLabel(string callsign, string label, string? notes, DateTimeOffset now)
    {
        var stationKey = NormalizeStationKey(callsign);
        var existing = tacticalLabels.GetValueOrDefault(stationKey);
        var tacticalLabel = new TacticalLabel(
            stationKey,
            label.Trim(),
            notes,
            existing?.CreatedAtUtc ?? now,
            now);

        tacticalLabels[stationKey] = tacticalLabel;
        RefreshStationDisplayName(stationKey);

        return tacticalLabel;
    }

    public bool RemoveTacticalLabel(string callsign)
    {
        var stationKey = NormalizeStationKey(callsign);
        var removed = tacticalLabels.Remove(stationKey);
        if (removed)
        {
            RefreshStationDisplayName(stationKey);
        }

        return removed;
    }

    public TacticalLabel? GetTacticalLabel(string callsign)
    {
        return tacticalLabels.TryGetValue(NormalizeStationKey(callsign), out var tacticalLabel)
            ? tacticalLabel
            : null;
    }

    public IReadOnlyCollection<TacticalLabel> GetAllTacticalLabels()
    {
        return tacticalLabels.Values
            .OrderBy(label => label.RealCallsign, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void ClearTacticalLabels()
    {
        tacticalLabels.Clear();
        foreach (var stationKey in stations.Keys.ToArray())
        {
            RefreshStationDisplayName(stationKey);
        }
    }

    public StationSnapshot? GetStation(string callsign)
    {
        return stations.TryGetValue(NormalizeStationKey(callsign), out var station)
            ? station
            : null;
    }

    public void UpdateAgeStates(DateTimeOffset now)
    {
        foreach (var (stationKey, station) in stations.ToArray())
        {
            stations[stationKey] = station with
            {
                LifecycleState = CalculateLifecycleState(station, now)
            };
        }
    }

    public bool HideStation(string callsign)
    {
        var stationKey = NormalizeStationKey(callsign);
        if (!stations.TryGetValue(stationKey, out var station))
        {
            return false;
        }

        stations[stationKey] = station with
        {
            IsManuallyHidden = true,
            LifecycleState = StationLifecycleState.Hidden
        };

        return true;
    }

    public bool UnhideStation(string callsign, DateTimeOffset now)
    {
        var stationKey = NormalizeStationKey(callsign);
        if (!stations.TryGetValue(stationKey, out var station))
        {
            return false;
        }

        var unhidden = station with { IsManuallyHidden = false };
        stations[stationKey] = unhidden with
        {
            LifecycleState = CalculateLifecycleState(unhidden, now)
        };

        return true;
    }

    public void ClearHiddenState(DateTimeOffset now)
    {
        foreach (var (stationKey, station) in stations.ToArray())
        {
            var unhidden = station with { IsManuallyHidden = false };
            stations[stationKey] = unhidden with
            {
                LifecycleState = CalculateLifecycleState(unhidden, now)
            };
        }
    }

    public bool ClearTrail(string callsign)
    {
        return trails.Remove(NormalizeStationKey(callsign));
    }

    public void ClearAllTrails()
    {
        trails.Clear();
    }

    public void Clear()
    {
        stations.Clear();
        trails.Clear();
    }

    private StationSnapshot CreateBaseUpdate(
        string stationKey,
        AprsPacket packet,
        AprsPacketSource packetSource,
        StationSnapshot? existing)
    {
        var (callsign, ssid) = SplitStationKey(stationKey);
        var realCallsign = FormatDisplayName(callsign, ssid);
        var tacticalLabel = GetTacticalLabelForKey(stationKey);

        return new StationSnapshot(
            callsign,
            ssid,
            realCallsign,
            tacticalLabel?.Label,
            tacticalLabel?.Label ?? realCallsign,
            existing?.IsManuallyHidden == true ? StationLifecycleState.Hidden : StationLifecycleState.Active,
            existing?.IsManuallyHidden ?? false,
            existing?.Latitude,
            existing?.Longitude,
            existing?.SymbolTableIdentifier,
            existing?.SymbolCode,
            existing?.Comment,
            packet.ReceivedAtUtc,
            packet.ReceivedAtUtc,
            packet.RawLine,
            packet.GetType().Name,
            existing?.CourseDegrees,
            existing?.SpeedKnots,
            existing?.AltitudeFeet,
            (existing?.PacketCount ?? 0) + 1,
            packet.Path,
            packetSource,
            existing?.HasMessagingCapability,
            existing?.Weather);
    }

    private static StationSnapshot ApplyPacketSpecificFields(StationSnapshot station, AprsPacket packet)
    {
        return packet switch
        {
            PositionAprsPacket position => station with
            {
                Latitude = position.Latitude ?? station.Latitude,
                Longitude = position.Longitude ?? station.Longitude,
                SymbolTableIdentifier = position.SymbolTableIdentifier ?? station.SymbolTableIdentifier,
                SymbolCode = position.SymbolCode ?? station.SymbolCode,
                Comment = string.IsNullOrEmpty(position.Comment) ? station.Comment : position.Comment,
                CourseDegrees = position.CourseDegrees ?? station.CourseDegrees,
                SpeedKnots = position.SpeedKnots ?? station.SpeedKnots,
                AltitudeFeet = position.AltitudeFeet ?? station.AltitudeFeet
            },
            StatusAprsPacket status => station with
            {
                Comment = status.StatusText
            },
            MessageAprsPacket => station with
            {
                HasMessagingCapability = true
            },
            ObjectAprsPacket aprsObject => station with
            {
                Latitude = aprsObject.Latitude ?? station.Latitude,
                Longitude = aprsObject.Longitude ?? station.Longitude,
                SymbolTableIdentifier = aprsObject.SymbolTableIdentifier ?? station.SymbolTableIdentifier,
                SymbolCode = aprsObject.SymbolCode ?? station.SymbolCode,
                Comment = string.IsNullOrEmpty(aprsObject.Comment) ? station.Comment : aprsObject.Comment
            },
            ItemAprsPacket item => station with
            {
                Latitude = item.Latitude ?? station.Latitude,
                Longitude = item.Longitude ?? station.Longitude,
                SymbolTableIdentifier = item.SymbolTableIdentifier ?? station.SymbolTableIdentifier,
                SymbolCode = item.SymbolCode ?? station.SymbolCode,
                Comment = string.IsNullOrEmpty(item.Comment) ? station.Comment : item.Comment
            },
            WeatherAprsPacket weather => station with
            {
                Latitude = weather.Latitude ?? station.Latitude,
                Longitude = weather.Longitude ?? station.Longitude,
                SymbolTableIdentifier = weather.SymbolTableIdentifier ?? station.SymbolTableIdentifier,
                SymbolCode = weather.SymbolCode ?? station.SymbolCode,
                Comment = string.IsNullOrEmpty(weather.Comment) ? station.Comment : weather.Comment,
                Weather = new StationWeatherSnapshot(
                    weather.WindDirectionDegrees,
                    weather.WindSpeedMph,
                    weather.WindGustMph,
                    weather.TemperatureFahrenheit,
                    weather.RainLastHourHundredthsInch,
                    weather.RainLast24HoursHundredthsInch,
                    weather.RainSinceMidnightHundredthsInch,
                    weather.HumidityPercent,
                    weather.BarometricPressureMillibars,
                    weather.LuminosityWattsPerSquareMeter,
                    weather.SnowHundredthsInch,
                    weather.RawWeatherBody,
                    string.IsNullOrEmpty(weather.Comment) ? null : weather.Comment)
            },
            _ => station
        };
    }

    private static string GetStationKey(AprsPacket packet)
    {
        return packet switch
        {
            ObjectAprsPacket aprsObject => NormalizeStationKey(aprsObject.ObjectName),
            ItemAprsPacket item => NormalizeStationKey(item.ItemName),
            _ => NormalizeStationKey(FormatDisplayName(packet.SourceCallsign, packet.SourceSsid))
        };
    }

    private static string NormalizeStationKey(string callsign)
    {
        return callsign.Trim().ToUpperInvariant();
    }

    private static string FormatDisplayName(string callsign, int? ssid)
    {
        return ssid is null ? callsign : $"{callsign}-{ssid}";
    }

    private static (string Callsign, int? Ssid) SplitStationKey(string stationKey)
    {
        var parts = stationKey.Split('-', 2);
        if (parts.Length == 2 && int.TryParse(parts[1], out var ssid))
        {
            return (parts[0], ssid);
        }

        return (stationKey, null);
    }

    private TacticalLabel? GetTacticalLabelForKey(string stationKey)
    {
        return tacticalLabels.TryGetValue(NormalizeStationKey(stationKey), out var tacticalLabel)
            ? tacticalLabel
            : null;
    }

    private void RefreshStationDisplayName(string stationKey)
    {
        var normalizedStationKey = NormalizeStationKey(stationKey);
        if (!stations.TryGetValue(normalizedStationKey, out var station))
        {
            return;
        }

        var tacticalLabel = GetTacticalLabelForKey(normalizedStationKey);
        stations[normalizedStationKey] = station with
        {
            TacticalLabel = tacticalLabel?.Label,
            DisplayName = tacticalLabel?.Label ?? station.RealCallsign
        };
    }

    private static IReadOnlyCollection<StationSnapshot> SortStations(IEnumerable<StationSnapshot> stationValues)
    {
        return stationValues
            .OrderBy(station => station.Callsign, StringComparer.OrdinalIgnoreCase)
            .ThenBy(station => station.Ssid)
            .ToArray();
    }

    private bool IsVisible(StationSnapshot station)
    {
        if (station.LifecycleState == StationLifecycleState.Hidden)
        {
            return agingConfiguration.IncludeHiddenStationsInNormalLists;
        }

        return station.LifecycleState != StationLifecycleState.Expired
            || agingConfiguration.ShowExpiredStations;
    }

    private StationLifecycleState CalculateLifecycleState(StationSnapshot station, DateTimeOffset now)
    {
        if (station.IsManuallyHidden)
        {
            return StationLifecycleState.Hidden;
        }

        var age = now - station.LastHeardUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age >= agingConfiguration.HiddenThreshold)
        {
            return StationLifecycleState.Hidden;
        }

        if (age >= agingConfiguration.ExpiredThreshold)
        {
            return StationLifecycleState.Expired;
        }

        if (age > agingConfiguration.ActiveThreshold && age < agingConfiguration.StaleThreshold)
        {
            return StationLifecycleState.Stale;
        }

        if (age >= agingConfiguration.StaleThreshold)
        {
            return StationLifecycleState.Expired;
        }

        return StationLifecycleState.Active;
    }

    private void AddTrailPointIfNeeded(string stationKey, AprsPacket packet, AprsPacketSource packetSource)
    {
        if (!trailConfiguration.TrailsEnabled)
        {
            return;
        }

        var trailPoint = CreateTrailPoint(stationKey, packet, packetSource);
        if (trailPoint is null)
        {
            return;
        }

        if (!trails.TryGetValue(stationKey, out var stationTrail))
        {
            stationTrail = [];
            trails[stationKey] = stationTrail;
        }

        if (stationTrail.Any(existing => IsDuplicateTrailPoint(existing, trailPoint)))
        {
            return;
        }

        if (trailConfiguration.MinimumDistanceMeters is not null
            && stationTrail.LastOrDefault() is { } latest
            && CalculateDistanceMeters(latest.Latitude, latest.Longitude, trailPoint.Latitude, trailPoint.Longitude) < trailConfiguration.MinimumDistanceMeters)
        {
            return;
        }

        stationTrail.Add(trailPoint);
        stationTrail.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        TrimTrail(stationTrail, trailPoint.Timestamp);
    }

    private static StationTrailPoint? CreateTrailPoint(string stationKey, AprsPacket packet, AprsPacketSource packetSource)
    {
        var (latitude, longitude, speed, course, altitude) = packet switch
        {
            PositionAprsPacket position => (position.Latitude, position.Longitude, position.SpeedKnots, position.CourseDegrees, position.AltitudeFeet),
            ObjectAprsPacket aprsObject => (aprsObject.Latitude, aprsObject.Longitude, null, null, null),
            ItemAprsPacket item => (item.Latitude, item.Longitude, null, null, null),
            WeatherAprsPacket weather => (weather.Latitude, weather.Longitude, null, null, null),
            _ => (null, null, null, null, null)
        };

        if (latitude is null || longitude is null)
        {
            return null;
        }

        return new StationTrailPoint(
            stationKey,
            latitude.Value,
            longitude.Value,
            packet.ReceivedAtUtc,
            speed,
            course,
            altitude,
            packetSource,
            packet.RawLine);
    }

    private void TrimTrail(List<StationTrailPoint> stationTrail, DateTimeOffset now)
    {
        if (trailConfiguration.MaximumTrailAge is not null)
        {
            var oldestAllowed = now - trailConfiguration.MaximumTrailAge.Value;
            stationTrail.RemoveAll(point => point.Timestamp < oldestAllowed);
        }

        if (trailConfiguration.MaximumTrailPointsPerStation < 1)
        {
            stationTrail.Clear();
            return;
        }

        while (stationTrail.Count > trailConfiguration.MaximumTrailPointsPerStation)
        {
            stationTrail.RemoveAt(0);
        }
    }

    private static bool IsDuplicateTrailPoint(StationTrailPoint existing, StationTrailPoint candidate)
    {
        return existing.Timestamp == candidate.Timestamp
            && existing.Latitude.Equals(candidate.Latitude)
            && existing.Longitude.Equals(candidate.Longitude);
    }

    private static double CalculateDistanceMeters(double firstLatitude, double firstLongitude, double secondLatitude, double secondLongitude)
    {
        const double earthRadiusMeters = 6_371_000;
        var firstLatitudeRadians = DegreesToRadians(firstLatitude);
        var secondLatitudeRadians = DegreesToRadians(secondLatitude);
        var latitudeDelta = DegreesToRadians(secondLatitude - firstLatitude);
        var longitudeDelta = DegreesToRadians(secondLongitude - firstLongitude);
        var halfChordLength = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(firstLatitudeRadians) * Math.Cos(secondLatitudeRadians)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var angularDistance = 2 * Math.Atan2(Math.Sqrt(halfChordLength), Math.Sqrt(1 - halfChordLength));

        return earthRadiusMeters * angularDistance;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    /// <summary>
    /// Restores a persisted snapshot directly into the in-memory store without running
    /// it through packet parsing. Called by the SQLite persistence layer on startup.
    /// Does not overwrite a snapshot that was heard more recently.
    /// </summary>
    public void RestoreSnapshot(StationSnapshot snapshot)
    {
        var key = NormalizeStationKey(snapshot.Callsign);
        if (stations.TryGetValue(key, out var existing) && existing.LastHeardUtc >= snapshot.LastHeardUtc)
        {
            return; // Already have a more-recent entry from this session.
        }

        stations[key] = snapshot;
    }

    /// <summary>
    /// Restores a persisted tactical label without triggering any update logic.
    /// Called by the SQLite persistence layer on startup.
    /// </summary>
    public void RestoreTacticalLabel(TacticalLabel label)
    {
        tacticalLabels[NormalizeStationKey(label.RealCallsign)] = label;
    }
}
