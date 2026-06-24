using Aprs.Desktop.ViewModels;
using Aprs.Services;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public class EventMonitorViewModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-10T09:27:51Z");

    [Fact]
    public void LoadsRecentEvents()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(3, viewModel.RowCount);
        Assert.Contains(viewModel.Rows, row => row.EventType == nameof(AprsEventType.StationUpdated));
    }

    [Fact]
    public void CategoryFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedCategoryFilter = nameof(AprsEventCategory.Weather);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(nameof(AprsEventCategory.Weather), row.Category);
    }

    [Fact]
    public void SeverityFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedSeverityFilter = nameof(AprsEventSeverity.Warning);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(nameof(AprsEventSeverity.Warning), row.Severity);
    }

    [Fact]
    public void EventTypeFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedEventTypeFilter = nameof(AprsEventType.PacketTransmitBlocked);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(nameof(AprsEventType.PacketTransmitBlocked), row.EventType);
    }

    [Fact]
    public void SearchFiltersBySummaryAndSource()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchText = "weather";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("WX9XYZ", row.Source);
    }

    [Fact]
    public void ClearHistoryClearsRows()
    {
        var viewModel = CreateViewModel();

        viewModel.ClearHistoryCommand.Execute(null);

        Assert.Equal(0, viewModel.RowCount);
    }

    private static EventMonitorViewModel CreateViewModel()
    {
        var bus = new AprsEventBus();
        bus.Publish(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station, "N0CALL", "Station updated.", Now));
        bus.Publish(CreateEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather, "WX9XYZ", "Weather updated.", Now.AddSeconds(1)));
        bus.Publish(CreateEvent(AprsEventType.PacketTransmitBlocked, AprsEventCategory.Packet, "N0CALL", "Transmit blocked.", Now.AddSeconds(2), AprsEventSeverity.Warning));
        return new EventMonitorViewModel(bus);
    }

    private static IAprsEvent CreateEvent(
        AprsEventType eventType,
        AprsEventCategory category,
        string sourceName,
        string summary,
        DateTimeOffset timestamp,
        AprsEventSeverity severity = AprsEventSeverity.Info)
    {
        var source = new ExternalSourceMetadata(
            sourceName,
            ExternalSourceType.Simulation,
            sourceName,
            timestamp,
            ContractDataOrigin.Simulated,
            ExternalTrustLevel.Internal);
        var metadata = AprsEventMetadata.Create(
            eventType,
            category,
            timestamp,
            source,
            severity,
            relatedCallsign: sourceName,
            summary: summary);

        return new AprsEventEnvelope<string>(metadata, summary);
    }
}
