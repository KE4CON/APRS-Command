using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class RawPacketLogViewModel : INotifyPropertyChanged
{
    private readonly IRawPacketLogService logService;
    private string searchText = string.Empty;
    private AprsPacketSource? packetSourceFilter;
    private RawPacketLogDirection? directionFilter;
    private string packetTypeFilter = "All";

    public RawPacketLogViewModel(IRawPacketLogService logService)
    {
        this.logService = logService;
        Rows = new ObservableCollection<RawPacketLogRowViewModel>();
        ClearLogCommand = new DesktopCommand(ClearLog);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RawPacketLogRowViewModel> Rows { get; }

    public DesktopCommand ClearLogCommand { get; }

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
        nameof(AprsPacketSource.External),
        nameof(AprsPacketSource.LocalGenerated)
    ];

    public IReadOnlyList<string> DirectionFilterOptions { get; } =
    [
        "All",
        nameof(RawPacketLogDirection.Received),
        nameof(RawPacketLogDirection.Transmitted),
        nameof(RawPacketLogDirection.Generated),
        nameof(RawPacketLogDirection.Blocked),
        nameof(RawPacketLogDirection.Unknown)
    ];

    public IReadOnlyList<string> PacketTypeFilterOptions { get; private set; } = ["All"];

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

    public string SelectedPacketSourceFilter
    {
        get => packetSourceFilter?.ToString() ?? "All";
        set
        {
            packetSourceFilter = Enum.TryParse<AprsPacketSource>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedDirectionFilter
    {
        get => directionFilter?.ToString() ?? "All";
        set
        {
            directionFilter = Enum.TryParse<RawPacketLogDirection>(value, out var parsed) ? parsed : null;
            Refresh();
            OnPropertyChanged();
        }
    }

    public string SelectedPacketTypeFilter
    {
        get => packetTypeFilter;
        set
        {
            packetTypeFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
            Refresh();
            OnPropertyChanged();
        }
    }

    public int RowCount => Rows.Count;

    public void Refresh()
    {
        var entries = logService.GetRecentEntries();
        var packetTypes = entries
            .Select(entry => entry.ParsedPacketType)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .Select(type => type!)
            .Prepend("All")
            .ToArray();
        PacketTypeFilterOptions = packetTypes;

        if (!string.Equals(packetTypeFilter, "All", StringComparison.OrdinalIgnoreCase)
            && !packetTypes.Contains(packetTypeFilter, StringComparer.OrdinalIgnoreCase))
        {
            packetTypeFilter = "All";
            OnPropertyChanged(nameof(SelectedPacketTypeFilter));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            entries = entries.Where(entry =>
                entry.RawPacketText.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase)
                || (entry.SourceCallsign?.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Notes?.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();
        }

        if (packetSourceFilter is not null)
        {
            entries = entries.Where(entry => entry.PacketSource == packetSourceFilter).ToArray();
        }

        if (directionFilter is not null)
        {
            entries = entries.Where(entry => entry.Direction == directionFilter).ToArray();
        }

        if (!string.Equals(packetTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            entries = entries.Where(entry => string.Equals(entry.ParsedPacketType, packetTypeFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        Rows.Clear();
        foreach (var row in entries.Select(entry => new RawPacketLogRowViewModel(entry)))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(PacketTypeFilterOptions));
        OnPropertyChanged(nameof(RowCount));
    }

    public static RawPacketLogViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new RawPacketLogService();
        service.AddReceivedRawPacket("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon", AprsPacketSource.AprsIs, "aprs-is", "APRS-IS", now.AddMinutes(-3));
        service.AddReceivedRawPacket("W1AW-9>APRS,WIDE1-1,WIDE2-1:=4123.45N/07234.56W>Mobile test", AprsPacketSource.TcpKiss, "tcp-kiss", "TCP KISS", now.AddMinutes(-2));
        service.AddGeneratedPacket("N0CALL>APRS:>Generated preview", timestampUtc: now.AddMinutes(-1), notes: "Beacon preview only");
        service.AddBlockedPacket("N0CALL>APRS:>Blocked RF transmit", AprsPacketSource.Rf, "rf-tx", "RF TX", now, "RF transmit disabled", "Blocked by safety gate");

        return new RawPacketLogViewModel(service);
    }

    private void ClearLog()
    {
        logService.ClearLog();
        Refresh();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
