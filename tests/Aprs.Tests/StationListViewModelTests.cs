using Aprs.Desktop.ViewModels;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class StationListViewModelTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_LoadsStationRows()
    {
        var viewModel = CreateStationList();

        Assert.Equal(4, viewModel.RowCount);
        Assert.Contains(viewModel.Rows, row => row.Callsign == "N0CALL" && row.DisplayName == "Net Control");
    }

    [Fact]
    public void Row_PreservesCallsignAndDisplayNameFields()
    {
        var viewModel = CreateStationList();

        var row = viewModel.Rows.Single(item => item.Callsign == "KD8ABC-7");

        Assert.Equal("KD8ABC-7", row.Callsign);
        Assert.Equal("Tactical 7", row.DisplayName);
        Assert.Equal("Car / mobile station", row.SymbolDescription);
    }

    [Fact]
    public void Row_HandlesMissingOptionalValuesSafely()
    {
        var viewModel = new StationListViewModel(new MapViewModel(
        [
            CreateMarker("N0CALL", "N0CALL", StationLifecycleState.Active, AprsPacketSource.Unknown, null, null, null)
        ]));

        var row = Assert.Single(viewModel.Rows);

        Assert.Equal("Unknown", row.Speed);
        Assert.Equal("Unknown", row.Course);
        Assert.Equal("Unknown", row.Distance);
        Assert.Equal("Unknown", row.Bearing);
        Assert.Equal("None", row.Comment);
    }

    [Fact]
    public void SearchText_FiltersByCallsign()
    {
        var viewModel = CreateStationList();

        viewModel.SearchText = "KD8";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("KD8ABC-7", row.Callsign);
    }

    [Fact]
    public void SearchText_FiltersByDisplayName()
    {
        var viewModel = CreateStationList();

        viewModel.SearchText = "weather";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("WX9XYZ", row.Callsign);
    }

    [Fact]
    public void ShowActiveOnly_FiltersOutNonActiveStations()
    {
        var viewModel = CreateStationList();

        viewModel.ShowActiveOnly = true;

        Assert.All(viewModel.Rows, row => Assert.Equal(StationLifecycleState.Active, row.AgeState));
        Assert.DoesNotContain(viewModel.Rows, row => row.Callsign == "WX9XYZ");
    }

    [Fact]
    public void ShowExpiredStations_WhenFalse_HidesExpiredRows()
    {
        var viewModel = CreateStationList();

        viewModel.ShowExpiredStations = false;

        Assert.DoesNotContain(viewModel.Rows, row => row.Callsign == "OLD1");
    }

    [Fact]
    public void PacketSourceFilter_FiltersRowsBySource()
    {
        var viewModel = CreateStationList();

        viewModel.SelectedPacketSourceFilter = nameof(AprsPacketSource.AprsIs);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("KD8ABC-7", row.Callsign);
        Assert.Equal(AprsPacketSource.AprsIs, row.PacketSource);
    }

    [Fact]
    public void SelectRow_UpdatesSharedMapSelectionAndDetails()
    {
        var map = CreateMap();
        var viewModel = new StationListViewModel(map);
        var row = viewModel.Rows.Single(item => item.Callsign == "KD8ABC-7");

        viewModel.SelectRow(row);

        Assert.Same(row, viewModel.SelectedRow);
        Assert.Equal("KD8ABC-7", map.SelectedStation?.Callsign);
        Assert.Equal("Tactical 7", map.SelectedStationDetails?.DisplayName);
    }

    [Fact]
    public void SortByDisplayName_OrdersRowsByDisplayName()
    {
        var viewModel = CreateStationList();

        viewModel.SortBy(StationListSortField.DisplayName);

        Assert.Equal("Net Control", viewModel.Rows.First().DisplayName);
    }

    [Fact]
    public void CreateCsvExportRows_ReturnsExportReadyRows()
    {
        var viewModel = CreateStationList();

        var rows = viewModel.CreateCsvExportRows();

        Assert.Equal("Callsign,DisplayName,Symbol,LastHeard,AgeState,PacketSource,Comment", rows[0]);
        Assert.Contains(rows, row => row.Contains("KD8ABC-7", StringComparison.Ordinal));
    }

    private static StationListViewModel CreateStationList()
    {
        return new StationListViewModel(CreateMap());
    }

    private static MapViewModel CreateMap()
    {
        return new MapViewModel(
        [
            CreateMarker("N0CALL", "Net Control", StationLifecycleState.Active, AprsPacketSource.Simulation, null, null, "Net control"),
            CreateMarker("KD8ABC-7", "Tactical 7", StationLifecycleState.Active, AprsPacketSource.AprsIs, 180, 12, "Mobile test"),
            CreateMarker("WX9XYZ", "Weather WX9XYZ", StationLifecycleState.Stale, AprsPacketSource.Simulation, null, null, "Weather station"),
            CreateMarker("OLD1", "OLD1", StationLifecycleState.Expired, AprsPacketSource.Replay, null, null, "Expired station")
        ]);
    }

    private static StationMarker CreateMarker(
        string callsign,
        string displayName,
        StationLifecycleState ageState,
        AprsPacketSource packetSource,
        int? course,
        int? speed,
        string? comment)
    {
        return StationMarker.Create(
            callsign,
            displayName,
            39.0,
            -84.0,
            '/',
            callsign == "WX9XYZ" ? '_' : '>',
            TestNow.AddMinutes(-10),
            ageState,
            packetSource,
            CourseDegrees: course,
            SpeedKnots: speed,
            comment: comment,
            packetCount: 1);
    }
}
