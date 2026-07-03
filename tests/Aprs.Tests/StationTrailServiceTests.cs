using System;
using Aprs.Desktop.Runtime;
using Xunit;

namespace Aprs.Tests;

public sealed class StationTrailServiceTests
{
    // Helper: add N unique positions for a callsign (spread 0.01 deg apart)
    private static void AddPoints(StationTrailService svc, string callsign, int count)
    {
        var t = DateTimeOffset.UtcNow;
        for (int i = 0; i < count; i++)
            svc.RecordPosition(callsign, 39.0 + i * 0.01, -84.5, t.AddSeconds(i));
    }

    [Fact]
    public void RecordPosition_SinglePoint_DoesNotAppearInGetTrails()
    {
        var svc = new StationTrailService();
        AddPoints(svc, "KE4CON", 1);
        Assert.Empty(svc.GetTrails());
    }

    [Fact]
    public void RecordPosition_TwoPoints_AppearInGetTrails()
    {
        var svc = new StationTrailService();
        AddPoints(svc, "KE4CON", 2);
        var trails = svc.GetTrails();
        Assert.Single(trails);
        Assert.Equal(2, trails["KE4CON"].Count);
    }

    [Fact]
    public void RecordPosition_MultipleStations_TrackedSeparately()
    {
        var svc = new StationTrailService();
        AddPoints(svc, "KE4CON", 2);
        AddPoints(svc, "W9ABC", 2);
        var trails = svc.GetTrails();
        Assert.Equal(2, trails.Count);
        Assert.True(trails.ContainsKey("KE4CON"));
        Assert.True(trails.ContainsKey("W9ABC"));
    }

    [Fact]
    public void RecordPosition_StationLookupIsCaseInsensitive()
    {
        var svc = new StationTrailService();
        var t = DateTimeOffset.UtcNow;
        svc.RecordPosition("ke4con", 39.0, -84.5, t);
        svc.RecordPosition("KE4CON", 39.1, -84.5, t.AddSeconds(1));
        Assert.Single(svc.GetTrails());
    }

    [Fact]
    public void RecordPosition_IgnoresDuplicatePosition()
    {
        var svc = new StationTrailService();
        var t = DateTimeOffset.UtcNow;
        svc.RecordPosition("KE4CON", 39.0, -84.5, t);
        svc.RecordPosition("KE4CON", 39.0, -84.5, t.AddSeconds(1)); // same coords
        svc.RecordPosition("KE4CON", 39.1, -84.5, t.AddSeconds(2));
        // 2 unique positions (duplicate was skipped)
        var trails = svc.GetTrails();
        Assert.Equal(2, trails["KE4CON"].Count);
    }

    [Fact]
    public void RecordPosition_IgnoresEmptyCallsign()
    {
        var svc = new StationTrailService();
        svc.RecordPosition("", 39.0, -84.5, DateTimeOffset.UtcNow);
        svc.RecordPosition("  ", 39.0, -84.5, DateTimeOffset.UtcNow);
        Assert.Empty(svc.GetTrails());
    }

    [Fact]
    public void RecordPosition_CapsAtMaxPointsPerStation()
    {
        var svc = new StationTrailService { MaxPointsPerStation = 5 };
        AddPoints(svc, "KE4CON", 10);
        Assert.Equal(5, svc.GetTrails()["KE4CON"].Count);
    }

    [Fact]
    public void RecordPosition_DropsOldestWhenOverLimit()
    {
        var svc = new StationTrailService { MaxPointsPerStation = 3 };
        var t = DateTimeOffset.UtcNow;
        svc.RecordPosition("KE4CON", 39.0, -84.5, t);
        svc.RecordPosition("KE4CON", 39.1, -84.5, t.AddSeconds(1));
        svc.RecordPosition("KE4CON", 39.2, -84.5, t.AddSeconds(2));
        svc.RecordPosition("KE4CON", 39.3, -84.5, t.AddSeconds(3)); // pushes out oldest

        var trail = svc.GetTrails()["KE4CON"];
        Assert.Equal(3, trail.Count);
        Assert.Equal(39.1, trail[0].Latitude, precision: 4); // 39.0 was dropped
    }

    [Fact]
    public void RecordPosition_DropsExpiredPoints()
    {
        // MaxAge of 1 minute so we can use clearly-old timestamps
        var svc = new StationTrailService { MaxAge = TimeSpan.FromMinutes(1) };
        var now = DateTimeOffset.UtcNow;
        var old = now.AddMinutes(-5); // clearly older than MaxAge

        svc.RecordPosition("KE4CON", 39.0, -84.5, old);
        // Adding a fresh point triggers pruning of stale points
        svc.RecordPosition("KE4CON", 39.1, -84.5, now);
        svc.RecordPosition("KE4CON", 39.2, -84.5, now.AddSeconds(1));

        var trails = svc.GetTrails();
        Assert.True(trails.ContainsKey("KE4CON"));
        Assert.Equal(2, trails["KE4CON"].Count); // old point pruned; 2 fresh remain
    }

    [Fact]
    public void TrailsUpdated_IsFiredOnNewPosition()
    {
        var svc = new StationTrailService();
        var fired = false;
        svc.TrailsUpdated += (_, _) => fired = true;
        svc.RecordPosition("KE4CON", 39.0, -84.5, DateTimeOffset.UtcNow);
        Assert.True(fired);
    }

    [Fact]
    public void TrailsUpdated_IsNotFiredOnDuplicatePosition()
    {
        var svc = new StationTrailService();
        var t = DateTimeOffset.UtcNow;
        svc.RecordPosition("KE4CON", 39.0, -84.5, t);

        var fired = false;
        svc.TrailsUpdated += (_, _) => fired = true;
        svc.RecordPosition("KE4CON", 39.0, -84.5, t.AddSeconds(5)); // same coords
        Assert.False(fired);
    }

    [Fact]
    public void Clear_RemovesAllTrails()
    {
        var svc = new StationTrailService();
        AddPoints(svc, "KE4CON", 2);
        svc.Clear();
        Assert.Empty(svc.GetTrails());
    }

    [Fact]
    public void Clear_FiresTrailsUpdated()
    {
        var svc = new StationTrailService();
        var fired = false;
        svc.TrailsUpdated += (_, _) => fired = true;
        svc.Clear();
        Assert.True(fired);
    }

    [Fact]
    public void GetTrails_ReturnsReadOnlyList_ForEachStation()
    {
        // GetTrails returns IReadOnlyList<TrailPoint> per station
        var svc = new StationTrailService();
        AddPoints(svc, "KE4CON", 3);
        var trails = svc.GetTrails();
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<TrailPoint>>(
            trails["KE4CON"]);
    }
}
