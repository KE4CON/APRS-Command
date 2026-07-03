using System;
using System.Linq;
using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class PacketStatisticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    // Minimal valid parsed packets for each type
    private static PositionAprsPacket Position(string callsign = "KE4CON") =>
        new(callsign + ">APRS:!3957.00N/08430.00W-Test",
            callsign, null, "APRS", [], "!3957.00N/08430.00W-Test",
            Now, true, [], null,
            '!', null, 39.95, -84.50, '/', '-', "Test",
            null, null, null, 0);

    private static MessageAprsPacket Message(string callsign = "KE4CON") =>
        new(callsign + ">APRS::W9ABC    :Hello{001}",
            callsign, null, "APRS", [], ":W9ABC    :Hello{001}",
            Now, true, [], null, "W9ABC", "Hello{001}", "Hello", "001",
            null, null, false, null, false, false, null);

    private static StatusAprsPacket Status(string callsign = "KE4CON") =>
        new(callsign + ">APRS:>Net control",
            callsign, null, "APRS", [], ">Net control",
            Now, true, [], null, "Net control", "Net control");

    private static RawAprsPacket Invalid() =>
        new("INVALID", "INVALID", null, "APRS", [], "INVALID",
            Now, false, ["bad packet"], null);

    [Fact]
    public void InitialState_AllCountsAreZero()
    {
        var svc = new PacketStatisticsService();
        Assert.Equal(0, svc.TotalPackets);
        Assert.Equal(0, svc.PositionPackets);
        Assert.Equal(0, svc.MessagePackets);
        Assert.Equal(0, svc.WeatherPackets);
        Assert.Equal(0, svc.ObjectPackets);
        Assert.Equal(0, svc.StatusPackets);
        Assert.Equal(0, svc.OtherPackets);
        Assert.Equal(0, svc.InvalidPackets);
        Assert.Equal(0, svc.UniqueStations);
    }

    [Fact]
    public void RecordPacket_IncrementsTotalPackets()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position());
        svc.RecordPacket(Position());
        Assert.Equal(2, svc.TotalPackets);
    }

    [Fact]
    public void RecordPacket_CountsByType()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position());
        svc.RecordPacket(Position());
        svc.RecordPacket(Message());
        svc.RecordPacket(Status());

        Assert.Equal(4, svc.TotalPackets);
        Assert.Equal(2, svc.PositionPackets);
        Assert.Equal(1, svc.MessagePackets);
        Assert.Equal(1, svc.StatusPackets);
        Assert.Equal(0, svc.WeatherPackets);
        Assert.Equal(0, svc.ObjectPackets);
        Assert.Equal(0, svc.OtherPackets);
    }

    [Fact]
    public void RecordPacket_InvalidPacket_CountsAsInvalidNotByType()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Invalid());
        Assert.Equal(1, svc.TotalPackets);
        Assert.Equal(1, svc.InvalidPackets);
        Assert.Equal(0, svc.PositionPackets);
        Assert.Equal(0, svc.UniqueStations);
    }

    [Fact]
    public void RecordPacket_TracksUniqueStations()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position("KE4CON"));
        svc.RecordPacket(Position("KE4CON")); // duplicate
        svc.RecordPacket(Position("W9ABC"));
        Assert.Equal(2, svc.UniqueStations);
    }

    [Fact]
    public void RecordPacket_StationLookupIsCaseInsensitive()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position("ke4con"));
        svc.RecordPacket(Position("KE4CON"));
        Assert.Equal(1, svc.UniqueStations);
    }

    [Fact]
    public void GetTopStations_ReturnsOrderedByCount()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position("KE4CON"));
        svc.RecordPacket(Position("KE4CON"));
        svc.RecordPacket(Position("KE4CON"));
        svc.RecordPacket(Position("W9ABC"));
        svc.RecordPacket(Position("W9ABC"));
        svc.RecordPacket(Position("KD9XYZ"));

        var top = svc.GetTopStations(3);
        Assert.Equal(3, top.Count);
        Assert.Equal("KE4CON", top[0].Callsign);
        Assert.Equal(3, top[0].Count);
        Assert.Equal("W9ABC", top[1].Callsign);
        Assert.Equal(2, top[1].Count);
    }

    [Fact]
    public void GetTopStations_RespectsNLimit()
    {
        var svc = new PacketStatisticsService();
        foreach (var call in new[] { "A", "B", "C", "D", "E" })
            svc.RecordPacket(Position(call));

        Assert.Equal(3, svc.GetTopStations(3).Count);
        Assert.Equal(5, svc.GetTopStations(10).Count);
    }

    [Fact]
    public void GetHourlyBuckets_Returns24Entries()
    {
        var svc = new PacketStatisticsService();
        Assert.Equal(24, svc.GetHourlyBuckets().Count);
    }

    [Fact]
    public void GetHourlyBuckets_ReflectsPacketsRecorded()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position());
        svc.RecordPacket(Position());
        var buckets = svc.GetHourlyBuckets();
        // The current hour bucket (last item, index 23 = most recent) should have 2
        Assert.Equal(2, buckets[23]);
    }

    [Fact]
    public void Reset_ClearsAllCounters()
    {
        var svc = new PacketStatisticsService();
        svc.RecordPacket(Position("KE4CON"));
        svc.RecordPacket(Message("W9ABC"));
        svc.RecordPacket(Invalid());

        svc.Reset();

        Assert.Equal(0, svc.TotalPackets);
        Assert.Equal(0, svc.PositionPackets);
        Assert.Equal(0, svc.MessagePackets);
        Assert.Equal(0, svc.InvalidPackets);
        Assert.Equal(0, svc.UniqueStations);
        Assert.All(svc.GetHourlyBuckets(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void PacketsPerMinute_IsZero_WhenNoPackets()
    {
        var svc = new PacketStatisticsService();
        Assert.Equal(0, svc.PacketsPerMinute);
    }

    [Fact]
    public void SessionDuration_IsPositive()
    {
        var svc = new PacketStatisticsService();
        Assert.True(svc.SessionDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void SessionStartUtc_IsSetOnConstruction()
    {
        var before = DateTimeOffset.UtcNow;
        var svc = new PacketStatisticsService();
        var after = DateTimeOffset.UtcNow;
        Assert.True(svc.SessionStartUtc >= before && svc.SessionStartUtc <= after);
    }
}
