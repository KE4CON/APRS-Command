using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class StationDatabaseTests
{
    private static readonly DateTimeOffset FirstHeardUtc = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondHeardUtc = FirstHeardUtc.AddMinutes(5);

    [Fact]
    public void ProcessPacket_CreatesNewStation_FromPositionPacket()
    {
        var database = new StationDatabase();
        var packet = Parse("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon", FirstHeardUtc);

        database.ProcessPacket(packet, AprsPacketSource.AprsIs);

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal("N0CALL", station.Callsign);
        Assert.Null(station.Ssid);
        Assert.Equal("N0CALL", station.RealCallsign);
        Assert.Null(station.TacticalLabel);
        Assert.Equal("N0CALL", station.DisplayName);
        Assert.Equal(StationLifecycleState.Active, station.LifecycleState);
        Assert.Equal(39.058333, station.Latitude!.Value, 6);
        Assert.Equal(-84.508333, station.Longitude!.Value, 6);
        Assert.Equal('/', station.SymbolTableIdentifier);
        Assert.Equal('-', station.SymbolCode);
        Assert.Equal("Test beacon", station.Comment);
        Assert.Equal(FirstHeardUtc, station.LastHeardUtc);
        Assert.Equal(FirstHeardUtc, station.LastPacketUtc);
        Assert.Equal("PositionAprsPacket", station.LastPacketType);
        Assert.Equal("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon", station.LastRawPacket);
        Assert.Equal(1, station.PacketCount);
        Assert.Equal(new[] { "TCPIP*" }, station.SourcePath);
        Assert.Equal(AprsPacketSource.AprsIs, station.PacketSource);
    }

    [Fact]
    public void ProcessPacket_UpdatesExistingStation_AndPreservesPositionForStatus()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.ProcessPacket(Parse("N0CALL>APRS:>Net control station online", SecondHeardUtc), AprsPacketSource.Rf);

        var station = database.GetStation("n0call");
        Assert.NotNull(station);
        Assert.Equal(39.058333, station.Latitude!.Value, 6);
        Assert.Equal(-84.508333, station.Longitude!.Value, 6);
        Assert.Equal("Net control station online", station.Comment);
        Assert.Equal(2, station.PacketCount);
        Assert.Equal(SecondHeardUtc, station.LastHeardUtc);
        Assert.Equal(SecondHeardUtc, station.LastPacketUtc);
        Assert.Equal("StatusAprsPacket", station.LastPacketType);
        Assert.Equal(AprsPacketSource.Rf, station.PacketSource);
    }

    [Fact]
    public void ProcessPacket_UpdatesCourseSpeedAltitude_FromLaterPosition()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("MOBILE-9>APRS:!3903.50N/08430.50W>Moving", FirstHeardUtc));

        database.ProcessPacket(Parse("MOBILE-9>APRS:!3904.50N/08431.50W>123/045/A=000789 Moving test", SecondHeardUtc));

        var station = database.GetStation("MOBILE-9");
        Assert.NotNull(station);
        Assert.Equal("MOBILE", station.Callsign);
        Assert.Equal(9, station.Ssid);
        Assert.Equal("MOBILE-9", station.DisplayName);
        Assert.Equal(39.075, station.Latitude!.Value, 6);
        Assert.Equal(-84.525, station.Longitude!.Value, 6);
        Assert.Equal(123, station.CourseDegrees);
        Assert.Equal(45, station.SpeedKnots);
        Assert.Equal(789, station.AltitudeFeet);
        Assert.Equal(2, station.PacketCount);
    }

    [Fact]
    public void ProcessPacket_MarksMessagingCapability_FromMessagePacket()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("K8ABC>APRS::N0CALL   :Hello there{01", FirstHeardUtc));

        var station = database.GetStation("K8ABC");
        Assert.NotNull(station);
        Assert.True(station.HasMessagingCapability);
        Assert.Equal("MessageAprsPacket", station.LastPacketType);
    }

    [Fact]
    public void ProcessPacket_CreatesObjectAndItemStations_WhenTheyHavePosition()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1", FirstHeardUtc));
        database.ProcessPacket(Parse("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater", SecondHeardUtc));

        var objectStation = database.GetStation("CHECKPNT1");
        var itemStation = database.GetStation("REPEATER");
        Assert.NotNull(objectStation);
        Assert.NotNull(itemStation);
        Assert.Equal("Checkpoint 1", objectStation.Comment);
        Assert.Equal("Local repeater", itemStation.Comment);
        Assert.Equal("ObjectAprsPacket", objectStation.LastPacketType);
        Assert.Equal("ItemAprsPacket", itemStation.LastPacketType);
    }

    [Fact]
    public void ProcessPacket_UpdatesWeatherStationState()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", FirstHeardUtc), AprsPacketSource.Simulation);

        var station = database.GetStation("WX9XYZ");
        Assert.NotNull(station);
        Assert.Equal(39.058333, station.Latitude!.Value, 6);
        Assert.Equal(-84.508333, station.Longitude!.Value, 6);
        Assert.NotNull(station.Weather);
        Assert.Equal(180, station.Weather.WindDirectionDegrees);
        Assert.Equal(5, station.Weather.WindSpeedMph);
        Assert.Equal(10, station.Weather.WindGustMph);
        Assert.Equal(72, station.Weather.TemperatureFahrenheit);
        Assert.Equal(1013.2, station.Weather.BarometricPressureMillibars);
        Assert.Equal(AprsPacketSource.Simulation, station.PacketSource);
    }

    [Fact]
    public void ProcessPacket_IgnoresMalformedPackets_WithoutCrashing()
    {
        var database = new StationDatabase();
        var malformedPacket = Parse("BADPOS>APRS:!9999.99N/99999.99W-Bad position", FirstHeardUtc);

        var exception = Record.Exception(() => database.ProcessPacket(malformedPacket));

        Assert.Null(exception);
        Assert.Empty(database.GetAllStations());
    }

    [Fact]
    public void Clear_RemovesAllStations()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.Clear();

        Assert.Empty(database.GetAllStations());
        Assert.Null(database.GetStation("N0CALL"));
    }

    [Fact]
    public void UpdateAgeStates_KeepsNewlyHeardStationActive()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.UpdateAgeStates(FirstHeardUtc.AddMinutes(30));

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal(StationLifecycleState.Active, station.LifecycleState);
    }

    [Fact]
    public void UpdateAgeStates_MarksStationStaleAfterActiveThreshold()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.UpdateAgeStates(FirstHeardUtc.AddMinutes(31));

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal(StationLifecycleState.Stale, station.LifecycleState);
    }

    [Fact]
    public void UpdateAgeStates_MarksStationExpiredAfterExpiredThreshold()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.UpdateAgeStates(FirstHeardUtc.AddHours(2));

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal(StationLifecycleState.Expired, station.LifecycleState);
    }

    [Fact]
    public void UpdateAgeStates_MarksStationHiddenAfterHiddenThreshold()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.UpdateAgeStates(FirstHeardUtc.AddHours(24));

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal(StationLifecycleState.Hidden, station.LifecycleState);
        Assert.False(station.IsManuallyHidden);
    }

    [Fact]
    public void HideStation_ManuallyHiddenStationsRemainHiddenAfterNewPacket()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        var hidden = database.HideStation("N0CALL");
        database.ProcessPacket(Parse("N0CALL>APRS:>Back on frequency", SecondHeardUtc));

        var station = database.GetStation("N0CALL");
        Assert.True(hidden);
        Assert.NotNull(station);
        Assert.True(station.IsManuallyHidden);
        Assert.Equal(StationLifecycleState.Hidden, station.LifecycleState);
        Assert.Equal("Back on frequency", station.Comment);
    }

    [Fact]
    public void UnhideStation_RestoresNormalAgeCalculation()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.HideStation("N0CALL");

        var unhidden = database.UnhideStation("N0CALL", FirstHeardUtc.AddMinutes(31));

        var station = database.GetStation("N0CALL");
        Assert.True(unhidden);
        Assert.NotNull(station);
        Assert.False(station.IsManuallyHidden);
        Assert.Equal(StationLifecycleState.Stale, station.LifecycleState);
    }

    [Fact]
    public void ClearHiddenState_UnhidesAllStationsAndRecalculatesAge()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc));
        database.HideStation("N0CALL");
        database.HideStation("W1AW");

        database.ClearHiddenState(FirstHeardUtc.AddMinutes(31));

        Assert.All(database.GetAllStations(), station =>
        {
            Assert.False(station.IsManuallyHidden);
            Assert.Equal(StationLifecycleState.Stale, station.LifecycleState);
        });
    }

    [Fact]
    public void GetVisibleStations_ExcludesHiddenStations()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc));
        database.HideStation("N0CALL");

        var visibleStations = database.GetVisibleStations();

        var visibleStation = Assert.Single(visibleStations);
        Assert.Equal("W1AW", visibleStation.Callsign);
    }

    [Fact]
    public void GetVisibleStations_CanExcludeExpiredStations()
    {
        var config = StationAgingConfiguration.Default with { ShowExpiredStations = false };
        var database = new StationDatabase(config);
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        database.UpdateAgeStates(FirstHeardUtc.AddHours(2));

        Assert.Empty(database.GetVisibleStations());
        Assert.Single(database.GetAllStations());
    }

    [Fact]
    public void GetVisibleStations_CanIncludeHiddenStationsWhenConfigured()
    {
        var config = StationAgingConfiguration.Default with { IncludeHiddenStationsInNormalLists = true };
        var database = new StationDatabase(config);
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.HideStation("N0CALL");

        var visibleStation = Assert.Single(database.GetVisibleStations());
        Assert.Equal(StationLifecycleState.Hidden, visibleStation.LifecycleState);
    }

    [Fact]
    public void GetActiveStations_ReturnsOnlyActiveStations()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc.AddHours(-1)));

        database.UpdateAgeStates(FirstHeardUtc);

        var activeStation = Assert.Single(database.GetActiveStations());
        Assert.Equal("N0CALL", activeStation.Callsign);
    }

    [Fact]
    public void ProcessPacket_AddsTrailPoint_FromPositionPacket()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon", FirstHeardUtc), AprsPacketSource.AprsIs);

        var trailPoint = Assert.Single(database.GetTrail("N0CALL"));
        Assert.Equal("N0CALL", trailPoint.Callsign);
        Assert.Equal(39.058333, trailPoint.Latitude, 6);
        Assert.Equal(-84.508333, trailPoint.Longitude, 6);
        Assert.Equal(FirstHeardUtc, trailPoint.Timestamp);
        Assert.Equal(AprsPacketSource.AprsIs, trailPoint.PacketSource);
        Assert.Equal("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon", trailPoint.RawPacket);
    }

    [Fact]
    public void ProcessPacket_AddsMultipleTrailPoints_InChronologicalOrder()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("N0CALL>APRS:!3905.50N/08432.50W-Later", SecondHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Earlier", FirstHeardUtc));

        var trail = database.GetTrail("N0CALL");
        Assert.Equal(2, trail.Count);
        Assert.Equal(FirstHeardUtc, trail[0].Timestamp);
        Assert.Equal(SecondHeardUtc, trail[1].Timestamp);
    }

    [Fact]
    public void ProcessPacket_CapturesMotionFields_InTrailPoint()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("MOBILE-9>APRS:!3904.50N/08431.50W>123/045/A=000789 Moving test", FirstHeardUtc), AprsPacketSource.Rf);

        var trailPoint = Assert.Single(database.GetTrail("MOBILE-9"));
        Assert.Equal("MOBILE-9", trailPoint.Callsign);
        Assert.Equal(45, trailPoint.SpeedKnots);
        Assert.Equal(123, trailPoint.CourseDegrees);
        Assert.Equal(789, trailPoint.AltitudeFeet);
        Assert.Equal(AprsPacketSource.Rf, trailPoint.PacketSource);
    }

    [Fact]
    public void ProcessPacket_DoesNotAddTrailPoint_ForStatusOrMessagePackets()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("N0CALL>APRS:>Net control station online", FirstHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS::K8ABC    :ack01", SecondHeardUtc));

        Assert.Empty(database.GetTrail("N0CALL"));
    }

    [Fact]
    public void ProcessPacket_KeepsTrailsSeparate_ForDifferentStations()
    {
        var database = new StationDatabase();

        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc));

        var firstTrail = Assert.Single(database.GetTrail("N0CALL"));
        var secondTrail = Assert.Single(database.GetTrail("W1AW"));
        Assert.Equal("N0CALL", firstTrail.Callsign);
        Assert.Equal("W1AW", secondTrail.Callsign);
    }

    [Fact]
    public void ProcessPacket_EnforcesTrailPointLimit()
    {
        var config = StationTrailConfiguration.Default with { MaximumTrailPointsPerStation = 2 };
        var database = new StationDatabase(config);

        database.ProcessPacket(Parse("N0CALL>APRS:!3901.00N/08430.00W-First", FirstHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS:!3902.00N/08431.00W-Second", FirstHeardUtc.AddMinutes(1)));
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.00N/08432.00W-Third", FirstHeardUtc.AddMinutes(2)));

        var trail = database.GetTrail("N0CALL");
        Assert.Equal(2, trail.Count);
        Assert.Equal(FirstHeardUtc.AddMinutes(1), trail[0].Timestamp);
        Assert.Equal(FirstHeardUtc.AddMinutes(2), trail[1].Timestamp);
    }

    [Fact]
    public void ProcessPacket_DoesNotAddDuplicateTrailPoint_ForSameLocationAndTimestamp()
    {
        var database = new StationDatabase();
        var packet = Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc);

        database.ProcessPacket(packet);
        database.ProcessPacket(packet);

        Assert.Single(database.GetTrail("N0CALL"));
    }

    [Fact]
    public void ProcessPacket_RespectsMinimumDistanceBeforeAddingTrailPoint()
    {
        var config = StationTrailConfiguration.Default with { MinimumDistanceMeters = 500 };
        var database = new StationDatabase(config);

        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-First", FirstHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.51N/08430.51W-Tiny move", SecondHeardUtc));

        Assert.Single(database.GetTrail("N0CALL"));
    }

    [Fact]
    public void ProcessPacket_TrimsTrailByMaximumAge()
    {
        var config = StationTrailConfiguration.Default with { MaximumTrailAge = TimeSpan.FromMinutes(5) };
        var database = new StationDatabase(config);

        database.ProcessPacket(Parse("N0CALL>APRS:!3901.00N/08430.00W-Old", FirstHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS:!3902.00N/08431.00W-New", FirstHeardUtc.AddMinutes(6)));

        var trailPoint = Assert.Single(database.GetTrail("N0CALL"));
        Assert.Equal(FirstHeardUtc.AddMinutes(6), trailPoint.Timestamp);
    }

    [Fact]
    public void ProcessPacket_DoesNotStoreTrails_WhenTrailsDisabled()
    {
        var config = StationTrailConfiguration.Default with { TrailsEnabled = false };
        var database = new StationDatabase(config);

        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        Assert.Empty(database.GetTrail("N0CALL"));
    }

    [Fact]
    public void ClearTrail_RemovesOnlyOneStationTrail()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc));

        var cleared = database.ClearTrail("N0CALL");

        Assert.True(cleared);
        Assert.Empty(database.GetTrail("N0CALL"));
        Assert.Single(database.GetTrail("W1AW"));
    }

    [Fact]
    public void ClearAllTrails_RemovesEveryTrail()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("W1AW>APRS:!4123.45N/07234.56W-Test beacon", FirstHeardUtc));

        database.ClearAllTrails();

        Assert.Empty(database.GetTrail("N0CALL"));
        Assert.Empty(database.GetTrail("W1AW"));
        Assert.Equal(2, database.GetAllStations().Count);
    }

    [Fact]
    public void SetTacticalLabel_UpdatesExistingStationDisplayName()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("KD8ABC-7>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));

        var label = database.SetTacticalLabel("KD8ABC-7", "Command Post", "Primary net station", SecondHeardUtc);

        var station = database.GetStation("kd8abc-7");
        Assert.NotNull(station);
        Assert.Equal("KD8ABC-7", station.RealCallsign);
        Assert.Equal("Command Post", station.TacticalLabel);
        Assert.Equal("Command Post", station.DisplayName);
        Assert.Equal("KD8ABC-7", label.RealCallsign);
        Assert.Equal("Primary net station", label.Notes);
        Assert.Equal(SecondHeardUtc, label.CreatedAtUtc);
        Assert.Equal(SecondHeardUtc, label.UpdatedAtUtc);
    }

    [Fact]
    public void SetTacticalLabel_UpdatesExistingLabelAndKeepsCreatedTimestamp()
    {
        var database = new StationDatabase();

        database.SetTacticalLabel("KD8ABC-7", "Old Label", null, FirstHeardUtc);
        var updated = database.SetTacticalLabel("kd8abc-7", "New Label", "Updated notes", SecondHeardUtc);

        Assert.Equal("KD8ABC-7", updated.RealCallsign);
        Assert.Equal("New Label", updated.Label);
        Assert.Equal("Updated notes", updated.Notes);
        Assert.Equal(FirstHeardUtc, updated.CreatedAtUtc);
        Assert.Equal(SecondHeardUtc, updated.UpdatedAtUtc);
    }

    [Fact]
    public void RemoveTacticalLabel_RestoresCallsignDisplayName()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("KD8ABC-7>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.SetTacticalLabel("KD8ABC-7", "Command Post", null, FirstHeardUtc);

        var removed = database.RemoveTacticalLabel("kd8abc-7");

        var station = database.GetStation("KD8ABC-7");
        Assert.True(removed);
        Assert.NotNull(station);
        Assert.Null(station.TacticalLabel);
        Assert.Equal("KD8ABC-7", station.DisplayName);
        Assert.Null(database.GetTacticalLabel("KD8ABC-7"));
    }

    [Theory]
    [InlineData("kd8abc-7")]
    [InlineData("KD8ABC-7")]
    [InlineData("Kd8Abc-7")]
    public void TacticalLabels_AreCaseInsensitiveByCallsign(string lookupCallsign)
    {
        var database = new StationDatabase();

        database.SetTacticalLabel("KD8ABC-7", "Command Post", null, FirstHeardUtc);

        var label = database.GetTacticalLabel(lookupCallsign);
        Assert.NotNull(label);
        Assert.Equal("Command Post", label.Label);
    }

    [Fact]
    public void SetTacticalLabel_BeforeStationHeard_AppliesWhenStationIsCreated()
    {
        var database = new StationDatabase();
        database.SetTacticalLabel("KD8ABC-7", "Command Post", null, FirstHeardUtc);

        database.ProcessPacket(Parse("kd8abc-7>APRS:!3903.50N/08430.50W-Test beacon", SecondHeardUtc));

        var station = database.GetStation("KD8ABC-7");
        Assert.NotNull(station);
        Assert.Equal("KD8ABC-7", station.RealCallsign);
        Assert.Equal("Command Post", station.TacticalLabel);
        Assert.Equal("Command Post", station.DisplayName);
    }

    [Fact]
    public void GetAllTacticalLabels_ReturnsAllLabels()
    {
        var database = new StationDatabase();
        database.SetTacticalLabel("KD8ABC-7", "Command Post", null, FirstHeardUtc);
        database.SetTacticalLabel("N0CALL", "Net Control", "Evening net", SecondHeardUtc);

        var labels = database.GetAllTacticalLabels();

        Assert.Equal(2, labels.Count);
        Assert.Contains(labels, label => label.RealCallsign == "KD8ABC-7" && label.Label == "Command Post");
        Assert.Contains(labels, label => label.RealCallsign == "N0CALL" && label.Label == "Net Control");
    }

    [Fact]
    public void ClearTacticalLabels_RemovesLabelsAndRefreshesStationDisplayNames()
    {
        var database = new StationDatabase();
        database.ProcessPacket(Parse("KD8ABC-7>APRS:!3903.50N/08430.50W-Test beacon", FirstHeardUtc));
        database.ProcessPacket(Parse("N0CALL>APRS:!3904.50N/08431.50W-Test beacon", FirstHeardUtc));
        database.SetTacticalLabel("KD8ABC-7", "Command Post", null, FirstHeardUtc);
        database.SetTacticalLabel("N0CALL", "Net Control", null, FirstHeardUtc);

        database.ClearTacticalLabels();

        Assert.Empty(database.GetAllTacticalLabels());
        Assert.Equal("KD8ABC-7", database.GetStation("KD8ABC-7")!.DisplayName);
        Assert.Equal("N0CALL", database.GetStation("N0CALL")!.DisplayName);
    }

    private static AprsPacket Parse(string rawLine, DateTimeOffset receivedAtUtc)
    {
        var parser = new AprsParser();
        parser.TryParse(rawLine, receivedAtUtc, out var packet, out _);

        return Assert.IsAssignableFrom<AprsPacket>(packet);
    }
}
