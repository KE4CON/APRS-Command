using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsObjectManagerTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptObject_LiveObjectPacketCreatesObject()
    {
        var manager = new AprsObjectManager();

        var state = manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"), AprsPacketSource.AprsIs);

        Assert.NotNull(state);
        Assert.Equal("CHECKPNT1", state.Name);
        Assert.Equal(AprsManagedObjectType.Object, state.ObjectType);
        Assert.Equal("OBJ1", state.OwnerCallsign);
        Assert.True(state.IsAlive);
        Assert.False(state.IsKilled);
        Assert.Equal(AprsObjectLifecycleState.Active, state.LifecycleState);
        Assert.Equal(39.058333, state.Latitude!.Value, 6);
        Assert.Equal(-84.508333, state.Longitude!.Value, 6);
        Assert.Equal('/', state.SymbolTableIdentifier);
        Assert.Equal('-', state.SymbolCode);
        Assert.Equal("Checkpoint 1", state.Comment);
        Assert.Equal("111111z", state.PacketTimestamp);
        Assert.Equal(AprsPacketSource.AprsIs, state.PacketSource);
        Assert.Empty(state.ValidationErrors);
    }

    [Fact]
    public void AcceptObject_LaterPacketWithSameNameUpdatesExistingObject()
    {
        var manager = new AprsObjectManager();
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));

        var updated = manager.AcceptObject(ParseObject(
            "OBJ2>APRS:;CHECKPNT1*092345z3904.50N/08431.50W>Updated checkpoint",
            TestNow.AddMinutes(5)));

        var state = Assert.Single(manager.GetAllObjects());
        Assert.Same(state, manager.GetObject("CHECKPNT1"));
        Assert.Equal(updated, state);
        Assert.Equal("OBJ2", state.OwnerCallsign);
        Assert.Equal("Updated checkpoint", state.Comment);
        Assert.Equal(TestNow, state.FirstHeardUtc);
        Assert.Equal(TestNow.AddMinutes(5), state.LastHeardUtc);
        Assert.Equal('>', state.SymbolCode);
    }

    [Fact]
    public void AcceptObject_KilledObjectMarksInactive()
    {
        var manager = new AprsObjectManager();

        var state = manager.AcceptObject(ParseObject("OBJ3>APRS:;HAZARD   _111111z3903.50N/08430.50W-Hazard cleared"));

        Assert.NotNull(state);
        Assert.Equal("HAZARD", state.Name);
        Assert.True(state.IsKilled);
        Assert.False(state.IsAlive);
        Assert.Equal(AprsObjectLifecycleState.Killed, state.LifecycleState);
        Assert.Empty(manager.GetActiveObjects(TestNow));
        Assert.Single(manager.GetKilledObjects());
    }

    [Fact]
    public void AcceptItem_ItemPacketCreatesItem()
    {
        var manager = new AprsObjectManager();

        var state = manager.AcceptItem(ParseItem("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater"), AprsPacketSource.Rf);

        Assert.NotNull(state);
        Assert.Equal("REPEATER", state.Name);
        Assert.Equal(AprsManagedObjectType.Item, state.ObjectType);
        Assert.Equal("ITEM1", state.OwnerCallsign);
        Assert.True(state.IsAlive);
        Assert.False(state.IsKilled);
        Assert.Equal('r', state.SymbolCode);
        Assert.Equal("Local repeater", state.Comment);
        Assert.Equal(AprsPacketSource.Rf, state.PacketSource);
    }

    [Fact]
    public void AcceptObject_TracksOwnerCallsignWithSsid()
    {
        var manager = new AprsObjectManager();

        var state = manager.AcceptObject(ParseObject("OBJ1-7>APRS:;NETCTRL  *092345z3903.50N/08430.50W>Net control object"));

        Assert.NotNull(state);
        Assert.Equal("OBJ1-7", state.OwnerCallsign);
    }

    [Fact]
    public void GetActiveObjects_ExcludesKilledObjects()
    {
        var manager = new AprsObjectManager();
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));
        manager.AcceptObject(ParseObject("OBJ3>APRS:;HAZARD   _111111z3903.50N/08430.50W-Hazard cleared"));

        var active = manager.GetActiveObjects(TestNow);

        var state = Assert.Single(active);
        Assert.Equal("CHECKPNT1", state.Name);
    }

    [Fact]
    public void Clear_RemovesAllObjects()
    {
        var manager = new AprsObjectManager();
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));
        manager.AcceptItem(ParseItem("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater"));

        manager.Clear();

        Assert.Empty(manager.GetAllObjects());
    }

    [Fact]
    public void RemoveObject_RemovesOneObjectOnly()
    {
        var manager = new AprsObjectManager();
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));
        manager.AcceptItem(ParseItem("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater"));

        var removed = manager.RemoveObject("checkpnt1");

        Assert.True(removed);
        var remaining = Assert.Single(manager.GetAllObjects());
        Assert.Equal("REPEATER", remaining.Name);
    }

    [Fact]
    public void AcceptPacket_MalformedObjectOrItemDoesNotCrash()
    {
        var manager = new AprsObjectManager();

        var objectException = Record.Exception(() => manager.AcceptPacket(Parse("BADOBJ>APRS:;SHORT")));
        var itemException = Record.Exception(() => manager.AcceptPacket(Parse("BADITEM>APRS:)BADITEM")));

        Assert.Null(objectException);
        Assert.Null(itemException);
        Assert.Equal(2, manager.GetAllObjects().Count);
        Assert.All(manager.GetAllObjects(), state => Assert.NotEmpty(state.ValidationErrors));
        Assert.Empty(manager.GetActiveObjects(TestNow));
    }

    [Fact]
    public void UpdateLifecycleStates_MarksObjectsStaleAndExpired()
    {
        var manager = new AprsObjectManager(new AprsObjectManagerConfiguration(
            StaleThreshold: TimeSpan.FromMinutes(30),
            ExpiredThreshold: TimeSpan.FromHours(2)));
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));

        manager.UpdateLifecycleStates(TestNow.AddMinutes(31));
        Assert.Equal(AprsObjectLifecycleState.Stale, manager.GetObject("CHECKPNT1")!.LifecycleState);

        manager.UpdateLifecycleStates(TestNow.AddHours(2));
        Assert.Equal(AprsObjectLifecycleState.Expired, manager.GetObject("CHECKPNT1")!.LifecycleState);
        Assert.Single(manager.GetInactiveObjects(TestNow.AddHours(2)));
    }

    [Fact]
    public void AdoptionPreparation_SetsLocalFlagsAndWarnings()
    {
        var manager = new AprsObjectManager();
        manager.AcceptObject(ParseObject("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"));

        var locallyCreated = manager.MarkLocallyCreated("CHECKPNT1", "N0CALL", TestNow.AddMinutes(1));
        var adopted = manager.AdoptObject("CHECKPNT1", "N0CALL", TestNow.AddMinutes(2));

        Assert.NotNull(locallyCreated);
        Assert.True(locallyCreated.IsLocallyCreated);
        Assert.False(locallyCreated.IsLocallyOwned);
        Assert.Contains("OBJ1", locallyCreated.OwnershipWarning);
        Assert.NotNull(adopted);
        Assert.True(adopted.IsAdopted);
        Assert.True(adopted.IsLocallyOwned);
        Assert.Contains("adopted", adopted.OwnershipWarning, StringComparison.OrdinalIgnoreCase);
    }

    private static AprsPacket Parse(string rawLine, DateTimeOffset? receivedAtUtc = null)
    {
        var parser = new AprsParser();
        return parser.Parse(rawLine, receivedAtUtc ?? TestNow);
    }

    private static ObjectAprsPacket ParseObject(string rawLine, DateTimeOffset? receivedAtUtc = null)
    {
        return Assert.IsType<ObjectAprsPacket>(Parse(rawLine, receivedAtUtc));
    }

    private static ItemAprsPacket ParseItem(string rawLine, DateTimeOffset? receivedAtUtc = null)
    {
        return Assert.IsType<ItemAprsPacket>(Parse(rawLine, receivedAtUtc));
    }
}
