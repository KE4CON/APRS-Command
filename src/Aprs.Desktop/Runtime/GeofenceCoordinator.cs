using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Subscribes to station position updates from the live data pipeline and
/// evaluates each update against all configured geofences. Fires toast
/// notifications when a station enters or leaves a geofenced area.
/// </summary>
public sealed class GeofenceCoordinator
{
    private readonly IGeofenceService geofenceService;
    private readonly Action<string, AlertSeverity>? toastCallback;

    /// <summary>
    /// Fired when a geofence event occurs — entry or exit of a station.
    /// UI can subscribe to show banners, play sounds, etc.
    /// </summary>
    public event EventHandler<GeofenceStationEvent>? GeofenceEventOccurred;

    public GeofenceCoordinator(
        IGeofenceService geofenceService,
        Action<string, AlertSeverity>? toastCallback = null)
    {
        this.geofenceService = geofenceService
            ?? throw new ArgumentNullException(nameof(geofenceService));
        this.toastCallback = toastCallback;
    }

    /// <summary>
    /// Called whenever a station's position is updated. Evaluates the new
    /// position against all geofences and fires events for any boundary crossings.
    /// </summary>
    public void OnStationPositionUpdated(
        string callsign,
        double latitude,
        double longitude,
        DateTimeOffset timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return;

        try
        {
            var events = geofenceService.EvaluateStationPosition(
                callsign, latitude, longitude, timestampUtc);

            foreach (var evt in events)
            {
                GeofenceEventOccurred?.Invoke(this, evt);
                toastCallback?.Invoke(evt.Summary, evt.AlertSeverity);
            }
        }
        catch
        {
            // Never let geofence evaluation crash the ingestion pipeline.
        }
    }
}
