using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class GeofenceRowViewModel
{
    public GeofenceRowViewModel(GeofenceDefinition geofence)
    {
        GeofenceId = geofence.GeofenceId;
        Name = string.IsNullOrWhiteSpace(geofence.Name) ? "Unnamed geofence" : geofence.Name;
        Type = geofence.GeofenceType.ToString();
        Enabled = geofence.Enabled ? "Enabled" : "Disabled";
        Center = geofence.CenterLatitude is null || geofence.CenterLongitude is null
            ? "-"
            : $"{geofence.CenterLatitude:0.00000}, {geofence.CenterLongitude:0.00000}";
        Radius = geofence.RadiusMeters is null ? "-" : $"{geofence.RadiusMeters:0} m";
        PointCount = geofence.PolygonPoints.Count.ToString();
        Alerts = $"{(geofence.AlertOnEnter ? "Enter" : "-")}/{(geofence.AlertOnExit ? "Exit" : "-")}";
        Validation = geofence.ValidationErrors.Count == 0 ? "Valid" : string.Join("; ", geofence.ValidationErrors);
    }

    public Guid GeofenceId { get; }

    public string Name { get; }

    public string Type { get; }

    public string Enabled { get; }

    public string Center { get; }

    public string Radius { get; }

    public string PointCount { get; }

    public string Alerts { get; }

    public string Validation { get; }
}
