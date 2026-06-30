using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class StationListViewModel : INotifyPropertyChanged
{
    private readonly MapViewModel map;
    private readonly List<StationListRowViewModel> allRows;
    private StationListRowViewModel? selectedRow;
    private string searchText = string.Empty;
    private bool showExpiredStations = true;
    private bool showActiveOnly;
    private AprsPacketSource? packetSourceFilter;
    private bool loraFilter;
    private StationListSortField sortField = StationListSortField.Callsign;

    public StationListViewModel(MapViewModel map)
    {
        this.map = map;
        allRows = map.Markers.Select(marker => new StationListRowViewModel(marker)).ToList();
        Rows = new ObservableCollection<StationListRowViewModel>();

        // Keep the list in sync with live marker updates pushed onto the map.
        map.Markers.CollectionChanged += (_, _) => RebuildFromMap();

        ApplyFiltersAndSort();
    }

    private void RebuildFromMap()
    {
        allRows.Clear();
        allRows.AddRange(map.Markers.Select(marker => new StationListRowViewModel(marker)));
        ApplyFiltersAndSort();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StationListRowViewModel> Rows { get; }

    public IReadOnlyList<string> PacketSourceFilterOptions { get; } =
    [
        "All",
        nameof(AprsPacketSource.Unknown),
        nameof(AprsPacketSource.AprsIs),
        nameof(AprsPacketSource.Rf),
        nameof(AprsPacketSource.TcpKiss),
        nameof(AprsPacketSource.SerialKiss),
        nameof(AprsPacketSource.Direwolf),
        nameof(AprsPacketSource.Agwpe),
        nameof(AprsPacketSource.Replay),
        nameof(AprsPacketSource.Simulation),
        "LoRa"
    ];

    public StationListRowViewModel? SelectedRow
    {
        get => selectedRow;
        private set
        {
            if (ReferenceEquals(selectedRow, value))
            {
                return;
            }

            selectedRow = value;
            OnPropertyChanged();
        }
    }

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
            ApplyFiltersAndSort();
            OnPropertyChanged();
        }
    }

    public bool ShowExpiredStations
    {
        get => showExpiredStations;
        set
        {
            if (showExpiredStations == value)
            {
                return;
            }

            showExpiredStations = value;
            ApplyFiltersAndSort();
            OnPropertyChanged();
        }
    }

    public bool ShowActiveOnly
    {
        get => showActiveOnly;
        set
        {
            if (showActiveOnly == value)
            {
                return;
            }

            showActiveOnly = value;
            ApplyFiltersAndSort();
            OnPropertyChanged();
        }
    }

    public string SelectedPacketSourceFilter
    {
        get => loraFilter ? "LoRa" : packetSourceFilter?.ToString() ?? "All";
        set
        {
            loraFilter = value == "LoRa";
            packetSourceFilter = !loraFilter && Enum.TryParse<AprsPacketSource>(value, out var parsed)
                ? parsed
                : null;
            ApplyFiltersAndSort();
            OnPropertyChanged();
        }
    }

    public int RowCount => Rows.Count;

    public void SortBy(StationListSortField field)
    {
        sortField = field;
        ApplyFiltersAndSort();
    }

    public void SelectRow(StationListRowViewModel row)
    {
        if (!Rows.Contains(row))
        {
            return;
        }

        SelectedRow = row;
        map.SelectStation(row.Marker);
        // Centre the map on this station so the operator can see it immediately.
        map.RequestCentreOnPosition(row.Marker.Latitude, row.Marker.Longitude);
    }

    public IReadOnlyList<string> CreateCsvExportRows()
    {
        return Rows
            .Select(row => string.Join(
                ',',
                Escape(row.Callsign),
                Escape(row.DisplayName),
                Escape(row.SymbolDescription),
                Escape(row.LastHeard),
                Escape(row.AgeStateLabel),
                Escape(row.PacketSourceLabel),
                Escape(row.Comment)))
            .Prepend("Callsign,DisplayName,Symbol,LastHeard,AgeState,PacketSource,Comment")
            .ToArray();
    }

    private void ApplyFiltersAndSort()
    {
        var rows = allRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var query = searchText.Trim();
            rows = rows.Where(row =>
                row.Callsign.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (showActiveOnly)
        {
            rows = rows.Where(row => row.AgeState == StationLifecycleState.Active);
        }

        if (!showExpiredStations)
        {
            rows = rows.Where(row => row.AgeState != StationLifecycleState.Expired);
        }

        if (packetSourceFilter is not null)
        {
            rows = rows.Where(row => row.PacketSource == packetSourceFilter);
        }

        if (loraFilter)
        {
            rows = rows.Where(row => row.IsLoRa);
        }

        rows = sortField switch
        {
            StationListSortField.DisplayName => rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase),
            StationListSortField.Distance => rows.OrderBy(row => row.Distance, StringComparer.OrdinalIgnoreCase),
            StationListSortField.LastHeard => rows.OrderByDescending(row => row.LastHeardUtc),
            StationListSortField.AgeState => rows.OrderBy(row => row.AgeState),
            StationListSortField.PacketSource => rows.OrderBy(row => row.PacketSource),
            _ => rows.OrderBy(row => row.Callsign, StringComparer.OrdinalIgnoreCase)
        };

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        if (selectedRow is not null && !Rows.Contains(selectedRow))
        {
            SelectedRow = null;
        }

        OnPropertyChanged(nameof(RowCount));
    }

    private static string Escape(string value)
    {
        return value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
