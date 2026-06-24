using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class DecodedEventLogViewModel : INotifyPropertyChanged
{
    private readonly IDecodedEventLogService eventLogService;
    private string searchText = string.Empty;
    private DecodedEventCategory? categoryFilter;
    private DecodedEventSeverity? severityFilter;
    private DecodedEventType? eventTypeFilter;
    private string callsignOrSourceFilter = string.Empty;

    public DecodedEventLogViewModel(IDecodedEventLogService eventLogService)
    {
        this.eventLogService = eventLogService;
        Rows = new ObservableCollection<DecodedEventLogRowViewModel>();
        ClearLogCommand = new DesktopCommand(ClearLog);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DecodedEventLogRowViewModel> Rows { get; }

    public DesktopCommand ClearLogCommand { get; }

    public IReadOnlyList<string> CategoryFilterOptions { get; } =
        ["All", .. Enum.GetNames<DecodedEventCategory>()];

    public IReadOnlyList<string> SeverityFilterOptions { get; } =
        ["All", .. Enum.GetNames<DecodedEventSeverity>()];

    public IReadOnlyList<string> EventTypeFilterOptions { get; } =
        ["All", .. Enum.GetNames<DecodedEventType>()];

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

    public string CallsignOrSourceFilter
    {
        get => callsignOrSourceFilter;
        set
        {
            if (callsignOrSourceFilter == value)
            {
                return;
            }

            callsignOrSourceFilter = value;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedCategoryFilter
    {
        get => categoryFilter?.ToString() ?? "All";
        set
        {
            categoryFilter = Enum.TryParse<DecodedEventCategory>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedSeverityFilter
    {
        get => severityFilter?.ToString() ?? "All";
        set
        {
            severityFilter = Enum.TryParse<DecodedEventSeverity>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedEventTypeFilter
    {
        get => eventTypeFilter?.ToString() ?? "All";
        set
        {
            eventTypeFilter = Enum.TryParse<DecodedEventType>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public int RowCount => Rows.Count;

    public void Refresh()
    {
        var entries = eventLogService.GetRecentEvents();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var query = searchText.Trim();
            entries = entries.Where(entry =>
                entry.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (entry.Details?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Notes?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(callsignOrSourceFilter))
        {
            var query = callsignOrSourceFilter.Trim();
            entries = entries.Where(entry =>
                (entry.SourceCallsign?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.RelatedEntity?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();
        }

        if (categoryFilter is not null)
        {
            entries = entries.Where(entry => entry.EventCategory == categoryFilter).ToArray();
        }

        if (severityFilter is not null)
        {
            entries = entries.Where(entry => entry.Severity == severityFilter).ToArray();
        }

        if (eventTypeFilter is not null)
        {
            entries = entries.Where(entry => entry.EventType == eventTypeFilter).ToArray();
        }

        Rows.Clear();
        foreach (var row in entries.Select(entry => new DecodedEventLogRowViewModel(entry)))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(RowCount));
    }

    public static DecodedEventLogViewModel CreateDesignTime()
    {
        var service = new DecodedEventLogService();
        service.AddStationEvent(DecodedEventType.StationCreated, "N0CALL", "Station N0CALL created.", AprsPacketSource.Simulation);
        service.AddWeatherEvent("WX9XYZ", "Weather station updated.", AprsPacketSource.Simulation, "Wind 180/005 gust 010");
        service.AddTransmitEvent(DecodedEventType.PacketTransmitBlocked, "RF transmit blocked by safety gate.", AprsPacketSource.Rf, "N0CALL", "RF transmit disabled.");
        service.AddGpsEvent("gpsd", "GPS fix updated.");

        return new DecodedEventLogViewModel(service);
    }

    private void ClearLog()
    {
        eventLogService.ClearEventLog();
        Refresh();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
