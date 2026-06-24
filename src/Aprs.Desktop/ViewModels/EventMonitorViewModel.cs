using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;
using AprsCommand.Contracts;

namespace Aprs.Desktop.ViewModels;

public sealed class EventMonitorViewModel : INotifyPropertyChanged
{
    private readonly IAprsEventBus eventBus;
    private string searchText = string.Empty;
    private AprsEventCategory? categoryFilter;
    private AprsEventSeverity? severityFilter;
    private AprsEventType? eventTypeFilter;

    public EventMonitorViewModel(IAprsEventBus eventBus)
    {
        this.eventBus = eventBus;
        Rows = new ObservableCollection<EventMonitorRowViewModel>();
        RefreshCommand = new DesktopCommand(Refresh);
        ClearHistoryCommand = new DesktopCommand(ClearHistory);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<EventMonitorRowViewModel> Rows { get; }

    public DesktopCommand RefreshCommand { get; }

    public DesktopCommand ClearHistoryCommand { get; }

    public IReadOnlyList<string> CategoryFilterOptions { get; } =
        ["All", .. Enum.GetNames<AprsEventCategory>()];

    public IReadOnlyList<string> SeverityFilterOptions { get; } =
        ["All", .. Enum.GetNames<AprsEventSeverity>()];

    public IReadOnlyList<string> EventTypeFilterOptions { get; } =
        ["All", .. Enum.GetNames<AprsEventType>()];

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value)
            {
                return;
            }

            searchText = value;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedCategoryFilter
    {
        get => categoryFilter?.ToString() ?? "All";
        set
        {
            categoryFilter = Enum.TryParse<AprsEventCategory>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedSeverityFilter
    {
        get => severityFilter?.ToString() ?? "All";
        set
        {
            severityFilter = Enum.TryParse<AprsEventSeverity>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedEventTypeFilter
    {
        get => eventTypeFilter?.ToString() ?? "All";
        set
        {
            eventTypeFilter = Enum.TryParse<AprsEventType>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public int RowCount => Rows.Count;

    public void Refresh()
    {
        IEnumerable<IAprsEvent> events = eventBus.GetRecentEvents();

        if (categoryFilter is not null)
        {
            events = events.Where(aprsEvent => aprsEvent.Metadata.EventCategory == categoryFilter);
        }

        if (severityFilter is not null)
        {
            events = events.Where(aprsEvent => aprsEvent.Metadata.Severity == severityFilter);
        }

        if (eventTypeFilter is not null)
        {
            events = events.Where(aprsEvent => aprsEvent.Metadata.EventType == eventTypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var query = searchText.Trim();
            events = events.Where(aprsEvent =>
                (aprsEvent.Metadata.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (aprsEvent.Metadata.Notes?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (aprsEvent.Metadata.RelatedCallsign?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (aprsEvent.Metadata.SourceMetadata.SourceName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Rows.Clear();
        foreach (var row in events.Select(aprsEvent => new EventMonitorRowViewModel(aprsEvent)))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(RowCount));
    }

    public static EventMonitorViewModel CreateDesignTime()
    {
        var bus = new AprsEventBus();
        var now = DateTimeOffset.UtcNow;
        bus.Publish(CreateDesignEvent(AprsEventType.StationUpdated, AprsEventCategory.Station, now.AddMinutes(-3), "N0CALL", "Station updated."));
        bus.Publish(CreateDesignEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather, now.AddMinutes(-2), "WX9XYZ", "Weather updated."));
        bus.Publish(CreateDesignEvent(AprsEventType.PacketTransmitBlocked, AprsEventCategory.Packet, now.AddMinutes(-1), "N0CALL", "Transmit blocked by safety gate.", AprsEventSeverity.Warning));
        return new EventMonitorViewModel(bus);
    }

    private void ClearHistory()
    {
        eventBus.ClearHistory();
        Refresh();
    }

    private static IAprsEvent CreateDesignEvent(
        AprsEventType eventType,
        AprsEventCategory category,
        DateTimeOffset timestamp,
        string sourceName,
        string summary,
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
