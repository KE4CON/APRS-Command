using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class GeofenceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidCircleGeofenceIsAccepted()
    {
        var service = CreateService();

        var geofence = service.CreateGeofence(CreateCircle(radiusMeters: 1000));

        Assert.Empty(geofence.ValidationErrors);
        Assert.Contains(geofence, service.GetEnabledGeofences());
    }

    [Fact]
    public void InvalidCircleRadiusIsRejected()
    {
        var service = CreateService();

        var geofence = service.CreateGeofence(CreateCircle(radiusMeters: -1));

        Assert.Contains(geofence.ValidationErrors, error => error.Contains("radius", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(service.GetEnabledGeofences());
    }

    [Fact]
    public void ValidPolygonGeofenceIsAccepted()
    {
        var service = CreateService();

        var geofence = service.CreateGeofence(CreatePolygon());

        Assert.Empty(geofence.ValidationErrors);
    }

    [Fact]
    public void PolygonWithTooFewPointsIsRejected()
    {
        var service = CreateService();

        var geofence = service.CreateGeofence(CreatePolygon([new GeofencePoint(39, -84), new GeofencePoint(39.1, -84.1)]));

        Assert.Contains(geofence.ValidationErrors, error => error.Contains("three", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PointInsideCircleReturnsTrue()
    {
        var service = CreateService();
        var geofence = service.CreateGeofence(CreateCircle(radiusMeters: 1000));

        Assert.True(service.ContainsPoint(geofence, 39.0585, -84.5085));
    }

    [Fact]
    public void PointOutsideCircleReturnsFalse()
    {
        var service = CreateService();
        var geofence = service.CreateGeofence(CreateCircle(radiusMeters: 100));

        Assert.False(service.ContainsPoint(geofence, 39.10, -84.50));
    }

    [Fact]
    public void PointInsidePolygonReturnsTrue()
    {
        var service = CreateService();
        var geofence = service.CreateGeofence(CreatePolygon());

        Assert.True(service.ContainsPoint(geofence, 39.060, -84.505));
    }

    [Fact]
    public void PointOutsidePolygonReturnsFalse()
    {
        var service = CreateService();
        var geofence = service.CreateGeofence(CreatePolygon());

        Assert.False(service.ContainsPoint(geofence, 39.100, -84.505));
    }

    [Fact]
    public void StationEnterEventIsDetected()
    {
        var service = CreateService();
        service.CreateGeofence(CreateCircle(radiusMeters: 1000));

        Assert.Empty(service.EvaluateStationPosition("N0CALL", 39.10, -84.50, Now));
        var events = service.EvaluateStationPosition("N0CALL", 39.0585, -84.5085, Now.AddMinutes(1));

        var geofenceEvent = Assert.Single(events);
        Assert.Equal(GeofenceEventType.Entered, geofenceEvent.EventType);
        Assert.Equal("N0CALL", geofenceEvent.StationCallsign);
    }

    [Fact]
    public void StationExitEventIsDetected()
    {
        var service = CreateService();
        service.CreateGeofence(CreateCircle(radiusMeters: 1000));

        Assert.Empty(service.EvaluateStationPosition("N0CALL", 39.0585, -84.5085, Now));
        var events = service.EvaluateStationPosition("N0CALL", 39.10, -84.50, Now.AddMinutes(1));

        Assert.Equal(GeofenceEventType.Left, Assert.Single(events).EventType);
    }

    [Fact]
    public void DisabledGeofenceDoesNotTrigger()
    {
        var service = CreateService();
        service.CreateGeofence(CreateCircle(radiusMeters: 1000) with { Enabled = false });

        service.EvaluateStationPosition("N0CALL", 39.10, -84.50, Now);
        var events = service.EvaluateStationPosition("N0CALL", 39.0585, -84.5085, Now.AddMinutes(1));

        Assert.Empty(events);
    }

    [Fact]
    public void GeofenceCanBeCreatedUpdatedAndDeleted()
    {
        var service = CreateService();
        var created = service.CreateGeofence(CreateCircle("Original", 1000));

        var updated = service.UpdateGeofence(created with { Name = "Updated" });

        Assert.NotNull(updated);
        Assert.Equal("Updated", service.GetGeofence(created.GeofenceId)?.Name);
        Assert.True(service.DeleteGeofence(created.GeofenceId));
        Assert.Empty(service.GetAllGeofences());
    }

    [Fact]
    public void EnabledGeofenceListExcludesDisabledAndInvalid()
    {
        var service = CreateService();
        service.CreateGeofence(CreateCircle("Enabled", 1000));
        service.CreateGeofence(CreateCircle("Disabled", 1000) with { Enabled = false });
        service.CreateGeofence(CreateCircle("Invalid", -1));

        var enabled = service.GetEnabledGeofences();

        var geofence = Assert.Single(enabled);
        Assert.Equal("Enabled", geofence.Name);
    }

    [Fact]
    public void AlertRuleIntegrationWorksForEnteredArea()
    {
        var geofenceService = CreateService();
        geofenceService.CreateGeofence(CreateCircle("Net area", 1000));
        var alertService = new AlertRuleService(new FakeClock { UtcNow = Now });
        alertService.AddRule(new AlertRule(Guid.NewGuid(), "Entered net area", true, AlertType.StationEnteredArea, AlertConditionType.Any, "Net area", AlertComparisonOperator.None, null, null, TimeSpan.Zero, AlertSeverity.Warning, AlertNotificationMethod.InApp, Now, Now, null, 0, null));
        geofenceService.EvaluateStationPosition("N0CALL", 39.10, -84.50, Now);
        var geofenceEvent = Assert.Single(geofenceService.EvaluateStationPosition("N0CALL", 39.0585, -84.5085, Now.AddMinutes(1)));

        var triggers = alertService.EvaluateGeofenceEvent(geofenceEvent);

        var trigger = Assert.Single(triggers);
        Assert.Equal(AlertType.StationEnteredArea, trigger.AlertType);
        Assert.Equal("N0CALL", trigger.SourceCallsignOrName);
    }

    private static GeofenceService CreateService()
    {
        return new GeofenceService(new FakeClock { UtcNow = Now });
    }

    private static GeofenceDefinition CreateCircle(double radiusMeters)
    {
        return CreateCircle("Circle", radiusMeters);
    }

    private static GeofenceDefinition CreateCircle(string name, double radiusMeters)
    {
        return new GeofenceDefinition(Guid.NewGuid(), name, "Test circle", true, GeofenceType.Circle, 39.05833, -84.50833, radiusMeters, [], Now, Now, null, "#2563EB", true, true, AlertSeverity.Warning, [], []);
    }

    private static GeofenceDefinition CreatePolygon(IReadOnlyList<GeofencePoint>? points = null)
    {
        return new GeofenceDefinition(
            Guid.NewGuid(),
            "Polygon",
            "Test polygon",
            true,
            GeofenceType.Polygon,
            null,
            null,
            null,
            points ?? [new GeofencePoint(39.050, -84.520), new GeofencePoint(39.070, -84.520), new GeofencePoint(39.070, -84.490), new GeofencePoint(39.050, -84.490)],
            Now,
            Now,
            null,
            "#16A34A",
            true,
            true,
            AlertSeverity.Advisory,
            [],
            []);
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
