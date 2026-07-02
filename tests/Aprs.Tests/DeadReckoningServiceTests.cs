using System;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DeadReckoningServiceTests
{
    // Build a minimal snapshot for testing
    private static StationSnapshot MakeSnapshot(
        double lat, double lon,
        int? courseDeg, int? speedKnots,
        DateTimeOffset? lastHeard = null) => new(
        Callsign:              "KE4CON-9",
        Ssid:                  9,
        RealCallsign:          "KE4CON",
        TacticalLabel:         null,
        DisplayName:           "KE4CON-9",
        LifecycleState:        StationLifecycleState.Active,
        IsManuallyHidden:      false,
        Latitude:              lat,
        Longitude:             lon,
        SymbolTableIdentifier: '/',
        SymbolCode:            '>',
        Comment:               null,
        LastHeardUtc:          lastHeard ?? DateTimeOffset.UtcNow.AddMinutes(-5),
        LastPacketUtc:         lastHeard ?? DateTimeOffset.UtcNow.AddMinutes(-5),
        LastRawPacket:         null,
        LastPacketType:        null,
        CourseDegrees:         courseDeg,
        SpeedKnots:            speedKnots,
        AltitudeFeet:          null,
        PacketCount:           1,
        DuplicatePacketCount:  0,
        SourcePath:            Array.Empty<string>(),
        PacketSource:          AprsPacketSource.AprsIs,
        HasMessagingCapability: null,
        Weather:               null);

    [Fact]
    public void Project_ReturnsNull_WhenNoCourseOrSpeed()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: null, speedKnots: null);
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public void Project_ReturnsNull_WhenSpeedIsZero()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 90, speedKnots: 0);
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public void Project_ReturnsNull_WhenDataTooOld()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 90, speedKnots: 30,
            lastHeard: DateTimeOffset.UtcNow.AddMinutes(-60));
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public void Project_ReturnsPosition_WhenMovingStationHeardRecently()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 0, speedKnots: 60,
            lastHeard: DateTimeOffset.UtcNow.AddMinutes(-10));
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal("KE4CON-9", result.Callsign);
        // Heading due north — latitude should be higher than origin
        Assert.True(result.Latitude > 35.0, "Projected latitude should be north of origin");
        // Longitude should be essentially unchanged when heading due north
        Assert.True(Math.Abs(result.Longitude - (-85.0)) < 0.01,
            "Longitude should not change significantly when heading due north");
    }

    [Fact]
    public void Project_DistanceIsReasonable_ForKnownSpeed()
    {
        // 60 knots for 60 minutes = 111.12 km (approximately 1 degree of latitude)
        var now = DateTimeOffset.UtcNow;
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 0, speedKnots: 60,
            lastHeard: now.AddMinutes(-60));

        // Use a 90-minute max age so this doesn't get filtered
        var result = DeadReckoningService.Project(snap, now, maxAgeMinutes: 90);

        Assert.NotNull(result);
        // 60 knots * 1 hour = 111.12 km
        Assert.True(result.DistanceKm > 100 && result.DistanceKm < 125,
            $"Expected ~111 km but got {result.DistanceKm:F1} km");
    }

    [Fact]
    public void Project_SummaryIsNonEmpty()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 270, speedKnots: 30,
            lastHeard: DateTimeOffset.UtcNow.AddMinutes(-5));
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.Contains("KE4CON-9", result.Summary);
    }

    [Fact]
    public void Project_HeadingEast_IncreasesLongitude()
    {
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 90, speedKnots: 60,
            lastHeard: DateTimeOffset.UtcNow.AddMinutes(-10));
        var result = DeadReckoningService.Project(snap, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.True(result.Longitude > -85.0, "Heading east should increase longitude");
    }

    [Fact]
    public void Project_DataAgeMatchesTimeSinceLastHeard()
    {
        var lastHeard = DateTimeOffset.UtcNow.AddMinutes(-15);
        var snap = MakeSnapshot(35.0, -85.0, courseDeg: 0, speedKnots: 30, lastHeard: lastHeard);
        var now = DateTimeOffset.UtcNow;
        var result = DeadReckoningService.Project(snap, now);

        Assert.NotNull(result);
        Assert.True(Math.Abs((result.DataAge - (now - lastHeard)).TotalSeconds) < 2,
            "DataAge should match elapsed time since last heard");
    }
}
