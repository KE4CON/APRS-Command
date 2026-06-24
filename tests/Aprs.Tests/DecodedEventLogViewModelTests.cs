using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DecodedEventLogViewModelTests
{
    [Fact]
    public void ViewModelLoadsEvents()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(4, viewModel.RowCount);
        Assert.Contains(viewModel.Rows, row => row.CallsignOrSource == "N0CALL");
    }

    [Fact]
    public void SearchFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchText = "pressure";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Weather", row.Category);
    }

    [Fact]
    public void CategoryFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedCategoryFilter = nameof(DecodedEventCategory.Port);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Port", row.Category);
    }

    [Fact]
    public void SeverityFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedSeverityFilter = nameof(DecodedEventSeverity.Warning);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Warning", row.Severity);
    }

    [Fact]
    public void EventTypeFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedEventTypeFilter = nameof(DecodedEventType.StationUpdated);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("StationUpdated", row.EventType);
    }

    [Fact]
    public void CallsignOrSourceFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.CallsignOrSourceFilter = "wx9xyz";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("WX9XYZ", row.CallsignOrSource);
    }

    [Fact]
    public void ClearCommandClearsEventLog()
    {
        var viewModel = CreateViewModel();

        viewModel.ClearLogCommand.Execute(null);

        Assert.Equal(0, viewModel.RowCount);
        Assert.Empty(viewModel.Rows);
    }

    private static DecodedEventLogViewModel CreateViewModel()
    {
        var service = new DecodedEventLogService();
        service.AddStationEvent(DecodedEventType.StationUpdated, "N0CALL", "Station updated.", AprsPacketSource.AprsIs);
        service.AddWeatherEvent("WX9XYZ", "Weather pressure rising.", AprsPacketSource.Rf, "Pressure trend");
        service.AddTransmitEvent(DecodedEventType.PacketTransmitBlocked, "Transmit blocked.", AprsPacketSource.Rf, "K8ABC");
        service.AddPortEvent(DecodedEventType.PortConnected, "TCP KISS", "Port connected.");

        return new DecodedEventLogViewModel(service);
    }
}
