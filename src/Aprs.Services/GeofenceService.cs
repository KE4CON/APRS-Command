namespace Aprs.Services;

public sealed class GeofenceService : IGeofenceService
{
    private const double EarthRadiusMeters = 6371000.0;
    private const double LargeGeofenceRadiusMeters = 500000.0;
    private readonly IBeaconSchedulerClock clock;
    private readonly List<GeofenceDefinition> geofences = [];
    private readonly Dictionary<string, bool> stationInsideState = new(StringComparer.OrdinalIgnoreCase);

    public GeofenceService(IBeaconSchedulerClock? clock = null)
    {
        this.clock = clock ?? new SystemBeaconSchedulerClock();
    }

    public GeofenceDefinition CreateGeofence(GeofenceDefinition geofence)
    {
        if (geofences.Any(existing => existing.GeofenceId == geofence.GeofenceId))
        {
            throw new InvalidOperationException("A geofence with the same ID already exists.");
        }

        var normalized = Normalize(geofence, geofence.CreatedAtUtc == default ? clock.UtcNow : geofence.CreatedAtUtc);
        geofences.Add(normalized);
        return normalized;
    }

    public GeofenceDefinition? UpdateGeofence(GeofenceDefinition geofence)
    {
        var index = geofences.FindIndex(existing => existing.GeofenceId == geofence.GeofenceId);
        if (index < 0)
        {
            return null;
        }

        var normalized = Normalize(geofence, geofence.CreatedAtUtc == default ? geofences[index].CreatedAtUtc : geofence.CreatedAtUtc);
        geofences[index] = normalized;
        return normalized;
    }

    public bool DeleteGeofence(Guid geofenceId)
    {
        stationInsideState.Keys
            .Where(key => key.StartsWith($"{geofenceId:N}|", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            .ToList()
            .ForEach(key => stationInsideState.Remove(key));

        return geofences.RemoveAll(geofence => geofence.GeofenceId == geofenceId) > 0;
    }

    public bool SetGeofenceEnabled(Guid geofenceId, bool enabled, DateTimeOffset? updatedAtUtc = null)
    {
        var index = geofences.FindIndex(geofence => geofence.GeofenceId == geofenceId);
        if (index < 0)
        {
            return false;
        }

        geofences[index] = Normalize(geofences[index] with
        {
            Enabled = enabled,
            UpdatedAtUtc = updatedAtUtc ?? clock.UtcNow
        }, geofences[index].CreatedAtUtc);
        return true;
    }

    public IReadOnlyList<GeofenceDefinition> GetAllGeofences()
    {
        return geofences.ToArray();
    }

    public IReadOnlyList<GeofenceDefinition> GetEnabledGeofences()
    {
        return geofences.Where(geofence => geofence.Enabled && geofence.ValidationErrors.Count == 0).ToArray();
    }

    public GeofenceDefinition? GetGeofence(Guid geofenceId)
    {
        return geofences.FirstOrDefault(geofence => geofence.GeofenceId == geofenceId);
    }

    public void ClearGeofences()
    {
        geofences.Clear();
        stationInsideState.Clear();
    }

    public GeofenceValidationResult ValidateGeofence(GeofenceDefinition geofence, bool allowLargeGeofence = false)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(geofence.Name))
        {
            errors.Add("Geofence name is required.");
        }

        switch (geofence.GeofenceType)
        {
            case GeofenceType.Circle:
                ValidateCoordinate(geofence.CenterLatitude, geofence.CenterLongitude, errors, "Circle center");
                if (geofence.RadiusMeters is null or <= 0)
                {
                    errors.Add("Circle radius must be positive.");
                }
                else if (!allowLargeGeofence && geofence.RadiusMeters > LargeGeofenceRadiusMeters)
                {
                    warnings.Add("Circle geofence is very large.");
                }

                break;
            case GeofenceType.Polygon:
                if (geofence.PolygonPoints.Count < 3)
                {
                    errors.Add("Polygon geofence requires at least three points.");
                }

                foreach (var point in geofence.PolygonPoints)
                {
                    ValidateCoordinate(point.Latitude, point.Longitude, errors, "Polygon point");
                }

                if (!allowLargeGeofence && EstimatePolygonSpanMeters(geofence.PolygonPoints) > LargeGeofenceRadiusMeters * 2)
                {
                    warnings.Add("Polygon geofence spans a very large area.");
                }

                break;
            case GeofenceType.RectanglePlaceholder:
                warnings.Add("Rectangle geofences are placeholders and are not evaluated yet.");
                break;
        }

        if (geofence.Enabled && errors.Count > 0)
        {
            errors.Add("Enabled geofence must be valid.");
        }

        return new GeofenceValidationResult(errors.Count == 0, errors, warnings);
    }

    public bool ContainsPoint(GeofenceDefinition geofence, double latitude, double longitude)
    {
        if (!IsValidLatitude(latitude) || !IsValidLongitude(longitude))
        {
            return false;
        }

        return geofence.GeofenceType switch
        {
            GeofenceType.Circle when geofence.CenterLatitude is not null && geofence.CenterLongitude is not null && geofence.RadiusMeters is not null =>
                CalculateDistanceMeters(geofence.CenterLatitude.Value, geofence.CenterLongitude.Value, latitude, longitude) <= geofence.RadiusMeters,
            GeofenceType.Polygon => IsPointInsidePolygon(latitude, longitude, geofence.PolygonPoints),
            _ => false
        };
    }

    public IReadOnlyList<GeofenceStationEvent> EvaluateStationPosition(string stationCallsign, double latitude, double longitude, DateTimeOffset timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(stationCallsign) || !IsValidLatitude(latitude) || !IsValidLongitude(longitude))
        {
            return [];
        }

        var events = new List<GeofenceStationEvent>();
        foreach (var geofence in GetEnabledGeofences())
        {
            var key = BuildStateKey(geofence.GeofenceId, stationCallsign);
            var wasKnown = stationInsideState.TryGetValue(key, out var wasInside);
            var isInside = ContainsPoint(geofence, latitude, longitude);
            stationInsideState[key] = isInside;

            if (!wasKnown)
            {
                continue;
            }

            if (!wasInside && isInside && geofence.AlertOnEnter)
            {
                events.Add(CreateEvent(geofence, stationCallsign, GeofenceEventType.Entered, timestampUtc, latitude, longitude));
            }
            else if (wasInside && !isInside && geofence.AlertOnExit)
            {
                events.Add(CreateEvent(geofence, stationCallsign, GeofenceEventType.Left, timestampUtc, latitude, longitude));
            }
        }

        return events;
    }

    public static double CalculateDistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);
        var deltaLat = DegreesToRadians(latitude2 - latitude1);
        var deltaLon = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private GeofenceDefinition Normalize(GeofenceDefinition geofence, DateTimeOffset createdAtUtc)
    {
        var validation = ValidateGeofence(geofence);
        return geofence with
        {
            Name = string.IsNullOrWhiteSpace(geofence.Name) ? geofence.Name : geofence.Name.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = geofence.UpdatedAtUtc == default ? clock.UtcNow : geofence.UpdatedAtUtc,
            PolygonPoints = geofence.PolygonPoints.ToArray(),
            ValidationErrors = validation.Errors,
            ValidationWarnings = validation.Warnings
        };
    }

    private static GeofenceStationEvent CreateEvent(
        GeofenceDefinition geofence,
        string callsign,
        GeofenceEventType eventType,
        DateTimeOffset timestamp,
        double latitude,
        double longitude)
    {
        var action = eventType == GeofenceEventType.Entered ? "entered" : "left";
        return new GeofenceStationEvent(
            Guid.NewGuid(),
            geofence.GeofenceId,
            geofence.Name,
            callsign,
            eventType,
            timestamp,
            latitude,
            longitude,
            geofence.AlertSeverity,
            $"{callsign} {action} geofence {geofence.Name}.",
            $"Station {callsign} {action} {geofence.Name} at {latitude:0.00000}, {longitude:0.00000}.");
    }

    private static bool IsPointInsidePolygon(double latitude, double longitude, IReadOnlyList<GeofencePoint> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var pi = points[i];
            var pj = points[j];
            var intersects = ((pi.Longitude > longitude) != (pj.Longitude > longitude))
                && (latitude < (pj.Latitude - pi.Latitude) * (longitude - pi.Longitude) / (pj.Longitude - pi.Longitude) + pi.Latitude);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double EstimatePolygonSpanMeters(IReadOnlyList<GeofencePoint> points)
    {
        if (points.Count < 2)
        {
            return 0;
        }

        var minLat = points.Min(point => point.Latitude);
        var maxLat = points.Max(point => point.Latitude);
        var minLon = points.Min(point => point.Longitude);
        var maxLon = points.Max(point => point.Longitude);
        return CalculateDistanceMeters(minLat, minLon, maxLat, maxLon);
    }

    private static void ValidateCoordinate(double? latitude, double? longitude, List<string> errors, string label)
    {
        if (latitude is null || longitude is null)
        {
            errors.Add($"{label} latitude and longitude are required.");
            return;
        }

        if (!IsValidLatitude(latitude.Value))
        {
            errors.Add($"{label} latitude is invalid.");
        }

        if (!IsValidLongitude(longitude.Value))
        {
            errors.Add($"{label} longitude is invalid.");
        }
    }

    private static bool IsValidLatitude(double latitude)
    {
        return latitude is >= -90 and <= 90;
    }

    private static bool IsValidLongitude(double longitude)
    {
        return longitude is >= -180 and <= 180;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static string BuildStateKey(Guid geofenceId, string callsign)
    {
        return $"{geofenceId:N}|{callsign.Trim().ToUpperInvariant()}";
    }
}
