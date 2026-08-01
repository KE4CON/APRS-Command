using Aprs.Desktop.ViewModels;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// The station-list metadata line shows only what's known, replacing the old "Unknown / Unknown /
/// Unknown" clutter for stations that aren't moving.
/// </summary>
public sealed class StationListRowMetaLineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static StationListRowViewModel Row(int? course, int? speed)
    {
        var marker = StationMarker.Create(
            "N0CALL", "N0CALL", 39.0, -84.0, '/', '-',
            Now, StationLifecycleState.Active, AprsPacketSource.Simulation,
            CourseDegrees: course, SpeedKnots: speed, altitudeFeet: null,
            lastPath: new[] { "WIDE1-1" }, comment: null, lastRawPacket: null, packetCount: 1);
        return new StationListRowViewModel(new StationMarkerViewModel(marker));
    }

    [Fact]
    public void StationaryStation_MetaLine_HasNoUnknownClutter()
    {
        var meta = Row(course: null, speed: null).MetaLine;

        Assert.DoesNotContain("Unknown", meta);
        Assert.DoesNotContain("·", meta); // age only, no separators
    }

    [Fact]
    public void MovingStation_MetaLine_IncludesSpeedAndCourse()
    {
        var meta = Row(course: 123, speed: 45).MetaLine;

        Assert.Contains("45 kt", meta);
        Assert.Contains("123°", meta);
        Assert.DoesNotContain("Unknown", meta);
    }
}
