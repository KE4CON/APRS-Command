using System;
using System.Collections.Generic;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for rectangle geofence evaluation — previously a placeholder,
/// now implemented using bounding-box lat/lon bounds derived from polygon points.
/// </summary>
public sealed class RectangleGeofenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly GeofenceService service = new();

    // Rectangle covering roughly the Cincinnati metro area:
    // SW corner: 38.95 N, -84.70 W
    // NE corner: 39.20 N, -84.30 W
    private static GeofenceDefinition CreateRectangle(
        string name = "Test Rectangle",
        bool enabled = true,
        bool alertOnEnter = true,
        bool alertOnExit = true)
    {
        var points = new[]
        {
            new GeofencePoint(38.95, -84.70), // SW
            new GeofencePoint(39.20, -84.70), // NW
            new GeofencePoint(39.20, -84.30), // NE
            new GeofencePoint(38.95, -84.30), // SE
        };

        return new GeofenceDefinition(
            Guid.NewGuid(), name, null, enabled,
            GeofenceType.Rectangle,
            CenterLatitude: null, CenterLongitude: null, RadiusMeters: null,
            points, Now, Now, null, null,
            alertOnEnter, alertOnExit, AlertSeverity.Warning, [], []);
    }

    // Two-point rectangle (just NW + SE corners)
    private static GeofenceDefinition CreateTwoPointRectangle()
    {
        var points = new[]
        {
            new GeofencePoint(38.95, -84.70), // SW
            new GeofencePoint(39.20, -84.30), // NE
        };

        return new GeofenceDefinition(
            Guid.NewGuid(), "Two-point rectangle", null, true,
            GeofenceType.Rectangle,
            null, null, null,
            points, Now, Now, null, null,
            true, true, AlertSeverity.Warning, [], []);
    }

    [Fact]
    public void ValidRectangle_PassesValidation()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.Empty(geofence.ValidationErrors);
    }

    [Fact]
    public void Rectangle_WithNoPoints_FailsValidation()
    {
        var geofence = service.CreateGeofence(new GeofenceDefinition(
            Guid.NewGuid(), "Empty", null, true, GeofenceType.Rectangle,
            null, null, null, [], Now, Now, null, null,
            true, true, AlertSeverity.Warning, [], []));

        Assert.NotEmpty(geofence.ValidationErrors);
        Assert.Contains(geofence.ValidationErrors, e => e.Contains("two corner"));
    }

    [Fact]
    public void Rectangle_WithOnePoint_FailsValidation()
    {
        var geofence = service.CreateGeofence(new GeofenceDefinition(
            Guid.NewGuid(), "One point", null, true, GeofenceType.Rectangle,
            null, null, null,
            [new GeofencePoint(39.0, -84.5)],
            Now, Now, null, null, true, true, AlertSeverity.Warning, [], []));

        Assert.NotEmpty(geofence.ValidationErrors);
    }

    [Fact]
    public void PointInsideRectangle_ReturnsTrue()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        // Cincinnati city centre — well inside the rectangle
        Assert.True(service.ContainsPoint(geofence, 39.10, -84.51));
    }

    [Fact]
    public void PointOutsideRectangle_ReturnsFalse()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        // Columbus, OH — north of the rectangle
        Assert.False(service.ContainsPoint(geofence, 39.96, -82.99));
    }

    [Fact]
    public void PointOnNorthBoundary_ReturnsTrue()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.True(service.ContainsPoint(geofence, 39.20, -84.50));
    }

    [Fact]
    public void PointOnSouthBoundary_ReturnsTrue()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.True(service.ContainsPoint(geofence, 38.95, -84.50));
    }

    [Fact]
    public void PointJustNorthOfRectangle_ReturnsFalse()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.False(service.ContainsPoint(geofence, 39.21, -84.50));
    }

    [Fact]
    public void PointJustSouthOfRectangle_ReturnsFalse()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.False(service.ContainsPoint(geofence, 38.94, -84.50));
    }

    [Fact]
    public void PointJustEastOfRectangle_ReturnsFalse()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.False(service.ContainsPoint(geofence, 39.10, -84.29));
    }

    [Fact]
    public void PointJustWestOfRectangle_ReturnsFalse()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.False(service.ContainsPoint(geofence, 39.10, -84.71));
    }

    [Fact]
    public void TwoPointRectangle_WorksIdenticallyToFourPoint()
    {
        var fourPoint = service.CreateGeofence(CreateRectangle());
        var twoPoint = service.CreateGeofence(CreateTwoPointRectangle());

        // Both should agree on a point inside
        Assert.True(service.ContainsPoint(fourPoint, 39.10, -84.51));
        Assert.True(service.ContainsPoint(twoPoint, 39.10, -84.51));

        // Both should agree on a point outside
        Assert.False(service.ContainsPoint(fourPoint, 40.00, -84.51));
        Assert.False(service.ContainsPoint(twoPoint, 40.00, -84.51));
    }

    [Fact]
    public void RectangleEnterEvent_IsDetected()
    {
        service.CreateGeofence(CreateRectangle());
        const string callsign = "KE4CON-9";
        const double lat = 39.10;
        const double lon = -84.51;

        // First evaluation — establishes baseline (no event)
        var first = service.EvaluateStationPosition(callsign, 40.00, -84.51, Now);
        Assert.Empty(first);

        // Second evaluation — station moves inside
        var entered = service.EvaluateStationPosition(callsign, lat, lon, Now.AddMinutes(5));
        Assert.Single(entered);
        Assert.Equal(GeofenceEventType.Entered, entered[0].EventType);
    }

    [Fact]
    public void RectangleExitEvent_IsDetected()
    {
        service.CreateGeofence(CreateRectangle());
        const string callsign = "KE4CON-9";

        // Station starts inside
        service.EvaluateStationPosition(callsign, 39.10, -84.51, Now);
        // Station moves outside
        var exited = service.EvaluateStationPosition(callsign, 40.00, -84.51, Now.AddMinutes(5));
        Assert.Single(exited);
        Assert.Equal(GeofenceEventType.Left, exited[0].EventType);
    }

    [Fact]
    public void DisabledRectangleGeofence_DoesNotTrigger()
    {
        service.CreateGeofence(CreateRectangle(enabled: false));
        service.EvaluateStationPosition("KE4CON-9", 40.00, -84.51, Now);
        var events = service.EvaluateStationPosition("KE4CON-9", 39.10, -84.51, Now.AddMinutes(5));
        Assert.Empty(events);
    }

    [Fact]
    public void Rectangle_NoLongerProducesPlaceholderWarning()
    {
        var geofence = service.CreateGeofence(CreateRectangle());
        Assert.DoesNotContain(geofence.ValidationWarnings,
            w => w.Contains("placeholder", StringComparison.OrdinalIgnoreCase));
    }
}
