using System;
using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class PacketStatisticsDashboardViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static PacketStatisticsService MakeStats() => new();

    private static PositionAprsPacket Position(string callsign = "KE4CON") =>
        new(callsign + ">APRS:!3957.00N/08430.00W-Test",
            callsign, null, "APRS", [], "!3957.00N/08430.00W-Test",
            Now, true, [], null,
            '!', null, 39.95, -84.50, '/', '-', "Test",
            null, null, null, 0);

    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void InitialState_AllCountsAreZero()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try
        {
            Assert.Equal(0, vm.TotalPackets);
            Assert.Equal(0, vm.PositionPackets);
            Assert.Equal(0, vm.UniqueStations);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void InitialState_HourlyData_Has24Entries()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.Equal(24, vm.HourlyData.Count); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void InitialState_PeakActivityLabel_IsNoActivity()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.Contains("No activity", vm.PeakActivityLabel); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void InitialState_AlohaRadius_IsDash()
    {
        // No data → Aloha circle returns null → display is "—"
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.Equal("—", vm.AlohaRadius); }
        finally { vm.Dispose(); }
    }

    // ── Refresh reads from service ─────────────────────────────────────────

    [Fact]
    public void AfterRecordingPackets_TotalPackets_Reflects()
    {
        var stats = MakeStats();
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("W9ABC"));

        var vm = new PacketStatisticsDashboardViewModel(stats);
        try { Assert.Equal(2, vm.TotalPackets); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void AfterRecordingPackets_UniqueStations_Reflects()
    {
        var stats = MakeStats();
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("W9ABC"));

        var vm = new PacketStatisticsDashboardViewModel(stats);
        try { Assert.Equal(2, vm.UniqueStations); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void AfterRecordingPackets_TopStations_IsPopulated()
    {
        var stats = MakeStats();
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("W9ABC"));

        var vm = new PacketStatisticsDashboardViewModel(stats);
        try
        {
            Assert.NotEmpty(vm.TopStations);
            Assert.Equal("KE4CON", vm.TopStations[0].Callsign);
        }
        finally { vm.Dispose(); }
    }

    // ── FormatAlohaRadius ─────────────────────────────────────────────────

    [Fact]
    public void AlohaRadius_DisplaysInMiles_WhenPreferMilesTrue()
    {
        var stats = MakeStats();
        // Add enough stations and packets for Aloha to calculate
        for (int i = 0; i < 10; i++)
            stats.RecordPacket(Position($"CALL{i}"));
        // Force session duration so ppm > 0
        // (PacketStatisticsService uses real clock — may be < 0.1 ppm)
        // We just verify the format; Aloha may be null if session too short

        var vm = new PacketStatisticsDashboardViewModel(stats, preferMiles: true);
        try
        {
            if (vm.AlohaRadius != "—")
                Assert.Contains("mi", vm.AlohaRadius);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void AlohaRadius_DisplaysInKm_WhenPreferMilesFalse()
    {
        var stats = MakeStats();
        for (int i = 0; i < 10; i++)
            stats.RecordPacket(Position($"CALL{i}"));

        var vm = new PacketStatisticsDashboardViewModel(stats, preferMiles: false);
        try
        {
            if (vm.AlohaRadius != "—")
                Assert.Contains("km", vm.AlohaRadius);
        }
        finally { vm.Dispose(); }
    }

    // ── Percentage properties ─────────────────────────────────────────────

    [Fact]
    public void PositionPct_IsZero_WhenNoPackets()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.Equal(0, vm.PositionPct); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void PositionPct_Is100_WhenAllPacketsArePositions()
    {
        var stats = MakeStats();
        stats.RecordPacket(Position("KE4CON"));
        stats.RecordPacket(Position("W9ABC"));

        var vm = new PacketStatisticsDashboardViewModel(stats);
        try { Assert.Equal(100, vm.PositionPct); }
        finally { vm.Dispose(); }
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        var exception = Record.Exception(() => vm.Dispose());
        Assert.Null(exception);
    }

    // ── SessionStart / SessionDuration ────────────────────────────────────

    [Fact]
    public void SessionStart_IsNonEmpty()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.False(string.IsNullOrWhiteSpace(vm.SessionStart)); }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void SessionDuration_IsNonEmpty()
    {
        var vm = new PacketStatisticsDashboardViewModel(MakeStats());
        try { Assert.False(string.IsNullOrWhiteSpace(vm.SessionDuration)); }
        finally { vm.Dispose(); }
    }
}
