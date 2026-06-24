using Aprs.Desktop.ViewModels;
using Aprs.Core;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class MapViewModelTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StationMarker_TryCreatePreservesCallsignAndDisplayName()
    {
        var station = CreateStation("KD8ABC-7", "Tactical 7", 39.1, -84.5);

        var created = StationMarker.TryCreate(station, out var marker);

        Assert.True(created);
        Assert.NotNull(marker);
        Assert.Equal("KD8ABC-7", marker.Callsign);
        Assert.Equal("Tactical 7", marker.DisplayName);
        Assert.Equal(39.1, marker.Latitude);
        Assert.Equal(-84.5, marker.Longitude);
        Assert.Equal("Car / mobile station", marker.SymbolDescription);
        Assert.Equal("car", marker.MarkerIconKey);
        Assert.Equal("C", marker.FallbackMarkerText);
    }

    [Fact]
    public void StationMarker_TryCreateRejectsStationWithoutPosition()
    {
        var station = CreateStation("KD8ABC-7", "KD8ABC-7", latitude: null, longitude: null);

        var created = StationMarker.TryCreate(station, out var marker);

        Assert.False(created);
        Assert.Null(marker);
    }

    [Fact]
    public void MapViewModel_CreateDesignTimeLoadsSampleMarkers()
    {
        var viewModel = MapViewModel.CreateDesignTime();

        Assert.True(viewModel.MarkerCount >= 3);
        Assert.Null(viewModel.SelectedStation);
        Assert.Null(viewModel.SelectedStationDetails);
        Assert.Contains(viewModel.Markers, marker => marker.Callsign == "N0CALL" && marker.DisplayName == "Net Control");
    }

    [Fact]
    public void MapViewModel_FromStationsLoadsPositionedStationsOnly()
    {
        var positioned = CreateStation("KD8ABC-7", "Tactical 7", 39.1, -84.5);
        var unpositioned = CreateStation("N0POS", "N0POS", latitude: null, longitude: null);

        var viewModel = MapViewModel.FromStations([positioned, unpositioned]);

        var marker = Assert.Single(viewModel.Markers);
        Assert.Equal("KD8ABC-7", marker.Callsign);
        Assert.Equal("Tactical 7", marker.DisplayName);
    }

    [Fact]
    public void SelectStation_UpdatesSelectedStation()
    {
        var viewModel = new MapViewModel(
        [
            CreateMarker("N0CALL", "Net Control", 39.0, -84.0),
            CreateMarker("W1AW-9", "Mobile 9", 41.0, -72.0)
        ]);

        var selected = viewModel.Markers.Single(marker => marker.Callsign == "W1AW-9");

        viewModel.SelectStation(selected);

        Assert.Same(selected, viewModel.SelectedStation);
        Assert.NotNull(viewModel.SelectedStationDetails);
        Assert.Equal("W1AW-9", viewModel.SelectedStationDetails.Callsign);
        Assert.Equal("Mobile 9", viewModel.SelectedStationDetails.DisplayName);
    }

    [Fact]
    public void ClearSelection_ClearsSelectedStationDetails()
    {
        var viewModel = new MapViewModel([CreateMarker("N0CALL", "Net Control", 39.0, -84.0)]);
        viewModel.SelectStation(viewModel.Markers.Single());

        viewModel.ClearSelection();

        Assert.Null(viewModel.SelectedStation);
        Assert.Null(viewModel.SelectedStationDetails);
        Assert.False(viewModel.HasSelectedStation);
    }

    [Fact]
    public void StationDetailsViewModel_FormatsMissingOptionalValuesSafely()
    {
        var marker = new StationMarkerViewModel(StationMarker.Create(
            "N0CALL",
            "N0CALL",
            39.0,
            -84.0,
            '/',
            '-',
            TestNow,
            StationLifecycleState.Active,
            AprsPacketSource.Unknown,
            CourseDegrees: null,
            SpeedKnots: null));

        var details = new StationDetailsViewModel(marker, TestNow.AddMinutes(10));

        Assert.Equal("N0CALL", details.Callsign);
        Assert.Equal("N0CALL", details.DisplayName);
        Assert.Equal("None", details.TacticalLabel);
        Assert.Equal("Unknown", details.SpeedCourse);
        Assert.Equal("Unknown", details.Altitude);
        Assert.Equal("None", details.LastPath);
        Assert.Equal("None", details.Comment);
        Assert.Equal("None", details.LastRawPacket);
        Assert.Equal("10 min ago", details.LastHeardAge);
    }

    [Fact]
    public void StationDetailsViewModel_FormatsAvailableStationFields()
    {
        var marker = new StationMarkerViewModel(StationMarker.Create(
            "KD8ABC-7",
            "Tactical 7",
            39.1,
            -84.5,
            '/',
            '>',
            TestNow,
            StationLifecycleState.Active,
            AprsPacketSource.AprsIs,
            CourseDegrees: 180,
            SpeedKnots: 12,
            altitudeFeet: 1234,
            lastPath: ["WIDE1-1", "WIDE2-1"],
            comment: "Mobile test",
            lastRawPacket: "KD8ABC-7>APRS:>Mobile test",
            packetCount: 5));

        var details = new StationDetailsViewModel(marker, TestNow.AddHours(2));

        Assert.Equal("KD8ABC-7", details.Callsign);
        Assert.Equal("Tactical 7", details.DisplayName);
        Assert.Equal("Tactical 7", details.TacticalLabel);
        Assert.Equal("12 kt / 180 deg", details.SpeedCourse);
        Assert.Equal("1234 ft", details.Altitude);
        Assert.Equal("WIDE1-1,WIDE2-1", details.LastPath);
        Assert.Equal("Mobile test", details.Comment);
        Assert.Equal("KD8ABC-7>APRS:>Mobile test", details.LastRawPacket);
        Assert.Equal("5", details.PacketCount);
        Assert.Equal("AprsIs", details.PacketSource);
    }

    [Fact]
    public void StationMarkerViewModel_ExposesMapCoordinatesWithinPercentRange()
    {
        var marker = new StationMarkerViewModel(CreateMarker("N0CALL", "Net Control", 39.0, -84.0));

        Assert.InRange(marker.MapLeftPercent, 0, 100);
        Assert.InRange(marker.MapTopPercent, 0, 100);
    }

    [Fact]
    public void ObjectMarker_TryCreatePreservesNameAndCoordinates()
    {
        var state = CreateObjectState("CHECKPNT1", 39.058333, -84.508333);

        var created = ObjectMarker.TryCreate(state, out var marker);

        Assert.True(created);
        Assert.NotNull(marker);
        Assert.Equal("CHECKPNT1", marker.ObjectName);
        Assert.Equal(AprsManagedObjectType.Object, marker.ObjectType);
        Assert.Equal(39.058333, marker.Latitude, 6);
        Assert.Equal(-84.508333, marker.Longitude, 6);
        Assert.Equal("House / home station", marker.SymbolDescription);
        Assert.Equal("home", marker.MarkerIconKey);
    }

    [Fact]
    public void ObjectMarkerViewModel_MarksKilledObjectInactive()
    {
        var state = CreateObjectState("HAZARD", 39.058333, -84.508333) with
        {
            IsAlive = false,
            IsKilled = true,
            LifecycleState = AprsObjectLifecycleState.Killed
        };

        var marker = new ObjectMarkerViewModel(ObjectMarker.TryCreate(state, out var objectMarker) ? objectMarker! : throw new InvalidOperationException());

        Assert.True(marker.IsInactive);
        Assert.Equal("Killed", marker.StateLabel);
    }

    [Fact]
    public void RefreshObjectMarkers_LoadsObjectsFromObjectManager()
    {
        var (map, manager) = CreateObjectMap();

        Assert.True(map.ObjectMarkerCount >= 2);
        Assert.Contains(map.ObjectMarkers, marker => marker.ObjectName == "CHECKPNT1");
        Assert.Contains(map.ObjectMarkers, marker => marker.ObjectName == "REPEATER");
        Assert.Equal(map.MarkerCount + map.ObjectMarkerCount, map.TotalMarkerCount);
        Assert.NotEmpty(manager.Objects);
    }

    [Fact]
    public void ClickToPlace_CreatesObjectDraftCoordinates()
    {
        var (map, manager) = CreateObjectMap();
        map.BeginCreateObjectPlacement();

        var placed = map.PlaceObjectAt(39.5, -84.25);

        Assert.True(placed);
        Assert.False(map.IsCreateObjectMode);
        Assert.Equal(39.5, manager.Editor.Latitude);
        Assert.Equal(-84.25, manager.Editor.Longitude);
        Assert.Contains("Draft", map.ObjectPlacementStatus);
    }

    [Fact]
    public void HandleMapClick_UsesPlaceholderCoordinateConverterForObjectDraft()
    {
        var (map, manager) = CreateObjectMap();
        map.BeginCreateObjectPlacement();

        var placed = map.HandleMapClick(50, 50);

        Assert.True(placed);
        Assert.Equal(0, manager.Editor.Latitude);
        Assert.Equal(0, manager.Editor.Longitude);
    }

    [Fact]
    public void MoveSelectedObjectTo_UpdatesLocalObjectCoordinates()
    {
        var (map, manager) = CreateObjectMap();
        var local = SaveLocalObject(manager, "LOCALOBJ", 39.0, -84.0);
        map.RefreshObjectMarkers();
        map.SelectObject(map.ObjectMarkers.Single(marker => marker.ObjectName == local.Name));

        var moved = map.MoveSelectedObjectTo(40.0, -85.0);

        Assert.True(moved);
        map.RefreshObjectMarkers();
        var marker = map.ObjectMarkers.Single(candidate => candidate.ObjectName == "LOCALOBJ");
        Assert.Equal(40.0, marker.Latitude, 6);
        Assert.Equal(-85.0, marker.Longitude, 6);
    }

    [Fact]
    public void MoveSelectedObjectTo_BlocksRemoteOwnedObjectWithoutAdoption()
    {
        var (map, _) = CreateObjectMap();
        map.SelectObject(map.ObjectMarkers.Single(marker => marker.ObjectName == "CHECKPNT1"));

        var moved = map.MoveSelectedObjectTo(40.0, -85.0);

        Assert.False(moved);
        Assert.Contains("adopt", map.ObjectPlacementStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoveSelectedObjectTo_AllowsAdoptedRemoteObject()
    {
        var (map, _) = CreateObjectMap();
        map.SelectObject(map.ObjectMarkers.Single(marker => marker.ObjectName == "CHECKPNT1"));

        var moved = map.MoveSelectedObjectTo(40.0, -85.0, adoptIfRemote: true);

        Assert.True(moved);
        map.RefreshObjectMarkers();
        var marker = map.ObjectMarkers.Single(candidate => candidate.ObjectName == "CHECKPNT1");
        Assert.Equal(40.0, marker.Latitude, 6);
        Assert.True(marker.IsAdopted);
    }

    [Fact]
    public void SelectObject_UpdatesSelectedObjectAndEditor()
    {
        var (map, manager) = CreateObjectMap();
        var marker = map.ObjectMarkers.Single(candidate => candidate.ObjectName == "REPEATER");

        map.SelectObject(marker);

        Assert.Same(marker, map.SelectedObject);
        Assert.True(map.HasSelectedObject);
        Assert.Null(map.SelectedStation);
        Assert.Equal("REPEATER", manager.Editor.ObjectName);
    }

    private static StationMarker CreateMarker(string callsign, string displayName, double latitude, double longitude)
    {
        return StationMarker.Create(
            callsign,
            displayName,
            latitude,
            longitude,
            '/',
            '>',
            TestNow,
            StationLifecycleState.Active,
            AprsPacketSource.Simulation,
            CourseDegrees: 180,
            SpeedKnots: 12);
    }

    private static AprsObjectState CreateObjectState(string name, double latitude, double longitude)
    {
        return new AprsObjectState(
            name,
            AprsManagedObjectType.Object,
            "OBJ1",
            IsAlive: true,
            IsKilled: false,
            AprsObjectLifecycleState.Active,
            latitude,
            longitude,
            '/',
            '-',
            Overlay: null,
            "Checkpoint",
            "111111z",
            TestNow,
            TestNow,
            TestNow,
            $"OBJ1>APRS:;{name.PadRight(9)}*111111z3903.50N/08430.50W-Checkpoint",
            AprsPacketSource.Simulation,
            IsLocallyCreated: false,
            IsLocallyOwned: false,
            IsAdopted: false,
            OwnershipWarning: null,
            ValidationErrors: []);
    }

    private static (MapViewModel Map, ObjectManagerViewModel Manager) CreateObjectMap()
    {
        var objectManager = new AprsObjectManager();
        var profileService = new LocalStationProfileService(TestNow);
        profileService.UpdateProfile(profileService.GetCurrentProfile() with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, TestNow);
        var parser = new AprsParser();
        objectManager.AcceptPacket(parser.Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1", TestNow), AprsPacketSource.Simulation);
        objectManager.AcceptPacket(parser.Parse("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater", TestNow), AprsPacketSource.Simulation);
        var manager = new ObjectManagerViewModel(objectManager, new AprsObjectEditorService(objectManager, profileService));
        var map = new MapViewModel([CreateMarker("N0CALL", "Net Control", 39.0, -84.0)]);
        map.AttachObjectManager(manager);
        return (map, manager);
    }

    private static AprsObjectState SaveLocalObject(ObjectManagerViewModel manager, string name, double latitude, double longitude)
    {
        manager.CreateNewObject();
        manager.Editor.ObjectName = name;
        manager.Editor.Latitude = latitude;
        manager.Editor.Longitude = longitude;
        manager.Editor.SymbolTableIdentifier = "/";
        manager.Editor.SymbolCode = "-";
        manager.Editor.Comment = "Local object";
        return manager.Save().ObjectState!;
    }

    private static StationSnapshot CreateStation(string callsign, string displayName, double? latitude, double? longitude)
    {
        return new StationSnapshot(
            Callsign: callsign.Split('-')[0],
            Ssid: callsign.Contains('-') ? 7 : null,
            RealCallsign: callsign,
            TacticalLabel: displayName == callsign ? null : displayName,
            DisplayName: displayName,
            LifecycleState: StationLifecycleState.Active,
            IsManuallyHidden: false,
            Latitude: latitude,
            Longitude: longitude,
            SymbolTableIdentifier: '/',
            SymbolCode: '>',
            Comment: "Test station",
            LastHeardUtc: TestNow,
            LastPacketUtc: TestNow,
            LastRawPacket: $"{callsign}>APRS:>Test",
            LastPacketType: "PositionAprsPacket",
            CourseDegrees: 180,
            SpeedKnots: 12,
            AltitudeFeet: 1234,
            PacketCount: 1,
            SourcePath: ["WIDE1-1"],
            PacketSource: AprsPacketSource.Simulation,
            HasMessagingCapability: true,
            Weather: null);
    }
}
