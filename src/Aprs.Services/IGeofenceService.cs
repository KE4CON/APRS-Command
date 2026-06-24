namespace Aprs.Services;

/// <summary>
/// Manages geofence definitions and evaluates station enter/exit transitions.
/// </summary>
public interface IGeofenceService
{
    GeofenceDefinition CreateGeofence(GeofenceDefinition geofence);

    GeofenceDefinition? UpdateGeofence(GeofenceDefinition geofence);

    bool DeleteGeofence(Guid geofenceId);

    bool SetGeofenceEnabled(Guid geofenceId, bool enabled, DateTimeOffset? updatedAtUtc = null);

    IReadOnlyList<GeofenceDefinition> GetAllGeofences();

    IReadOnlyList<GeofenceDefinition> GetEnabledGeofences();

    GeofenceDefinition? GetGeofence(Guid geofenceId);

    void ClearGeofences();

    GeofenceValidationResult ValidateGeofence(GeofenceDefinition geofence, bool allowLargeGeofence = false);

    bool ContainsPoint(GeofenceDefinition geofence, double latitude, double longitude);

    IReadOnlyList<GeofenceStationEvent> EvaluateStationPosition(string stationCallsign, double latitude, double longitude, DateTimeOffset timestampUtc);
}
