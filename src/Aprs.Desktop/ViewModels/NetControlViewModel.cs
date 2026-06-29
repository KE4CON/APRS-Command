using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Net control roster — tracks check-in status, last heard time, and position
/// for all stations operating under net control. Operators can manually check
/// stations in/out, mark them on standby, and add notes.
///
/// <para>Stations appear in the roster in two ways:
/// (1) manually added by the net control operator, or
/// (2) automatically added when they are heard on APRS-IS or RF.</para>
/// </summary>
public sealed class NetControlViewModel : INotifyPropertyChanged
{
    private readonly IStationDatabase stationDatabase;
    private NetControlRosterEntry? selectedEntry;
    private string filterText = string.Empty;
    private string netName = "Net Control";
    private string statusText = string.Empty;
    private bool autoAddHeardStations;

    public NetControlViewModel(IStationDatabase stationDatabase)
    {
        this.stationDatabase = stationDatabase ?? throw new ArgumentNullException(nameof(stationDatabase));
        Roster = [];
        FilteredRoster = [];

        CheckInCommand       = new DesktopCommand(CheckInSelected);
        StandbyCommand       = new DesktopCommand(StandbySelected);
        DepartCommand        = new DesktopCommand(DepartSelected);
        SetAvailableCommand  = new DesktopCommand(() => SetResourceStatus(ResourceStatus.Available));
        SetAssignedCommand   = new DesktopCommand(() => SetResourceStatus(ResourceStatus.Assigned));
        SetOnSceneCommand    = new DesktopCommand(() => SetResourceStatus(ResourceStatus.OnScene));
        SetReturningCommand  = new DesktopCommand(() => SetResourceStatus(ResourceStatus.Returning));
        SetOutOfServiceCommand = new DesktopCommand(() => SetResourceStatus(ResourceStatus.OutOfService));
        RemoveCommand     = new DesktopCommand(RemoveSelected);
        ClearAllCommand   = new DesktopCommand(ClearAll);
        RefreshCommand    = new DesktopCommand(RefreshFromDatabase);
        AddCallsignCommand = new DesktopCommand(AddCallsign);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Collections ───────────────────────────────────────────────────

    public ObservableCollection<NetControlRosterEntry> Roster { get; }
    public ObservableCollection<NetControlRosterEntry> FilteredRoster { get; }

    // ── Properties ────────────────────────────────────────────────────

    public string NetName
    {
        get => netName;
        set { if (netName != value) { netName = value; OnPropertyChanged(); } }
    }

    public string FilterText
    {
        get => filterText;
        set { if (filterText != value) { filterText = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    public NetControlRosterEntry? SelectedEntry
    {
        get => selectedEntry;
        set { if (selectedEntry != value) { selectedEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); } }
    }

    public bool HasSelection => selectedEntry is not null;

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool AutoAddHeardStations
    {
        get => autoAddHeardStations;
        set { if (autoAddHeardStations != value) { autoAddHeardStations = value; OnPropertyChanged(); } }
    }

    // Callsign input for manual add
    private string newCallsign = string.Empty;
    public string NewCallsign
    {
        get => newCallsign;
        set { if (newCallsign != value) { newCallsign = value; OnPropertyChanged(); } }
    }

    // ── Summary counts ────────────────────────────────────────────────

    public int TotalCount      => Roster.Count;
    public int CheckedInCount  => Roster.Count(e => e.Status == CheckInStatus.CheckedIn);
    public int StandbyCount    => Roster.Count(e => e.Status == CheckInStatus.Standby);
    public int DepartedCount   => Roster.Count(e => e.Status == CheckInStatus.Departed);

    public string SummaryText  =>
        $"{CheckedInCount} checked in · {StandbyCount} standby · {DepartedCount} departed · {TotalCount} total";

    // ── Commands ──────────────────────────────────────────────────────

    public IReadOnlyList<ResourceType> AvailableResourceTypes { get; } = [ResourceType.Untyped, ResourceType.Type1, ResourceType.Type2, ResourceType.Type3, ResourceType.Type4];

    public DesktopCommand CheckInCommand       { get; }
    public DesktopCommand StandbyCommand       { get; }
    public DesktopCommand DepartCommand        { get; }
    public DesktopCommand SetAvailableCommand  { get; }
    public DesktopCommand SetAssignedCommand   { get; }
    public DesktopCommand SetOnSceneCommand    { get; }
    public DesktopCommand SetReturningCommand  { get; }
    public DesktopCommand SetOutOfServiceCommand { get; }
    public DesktopCommand RemoveCommand      { get; }
    public DesktopCommand ClearAllCommand    { get; }
    public DesktopCommand RefreshCommand     { get; }
    public DesktopCommand AddCallsignCommand { get; }

    // ── Public methods ────────────────────────────────────────────────

    /// <summary>
    /// Called by the runtime when a packet is received so the roster can update
    /// last-heard times and positions, and optionally auto-add new stations.
    /// </summary>
    public void ProcessHeardStation(string callsign, double? latitude, double? longitude,
        string? comment, DateTimeOffset heardAt)
    {
        var existing = Roster.FirstOrDefault(e =>
            string.Equals(e.Callsign, callsign, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.LastHeardUtc = heardAt;
            if (latitude.HasValue)  existing.Latitude  = latitude;
            if (longitude.HasValue) existing.Longitude = longitude;
            if (!string.IsNullOrWhiteSpace(comment)) existing.Comment = comment!;
            RefreshSummary();
        }
        else if (AutoAddHeardStations)
        {
            AddToRoster(callsign, latitude, longitude, comment ?? string.Empty, heardAt);
        }
    }

    /// <summary>Refreshes last-heard times from the live station database.</summary>
    public void RefreshFromDatabase()
    {
        var stations = stationDatabase.GetAllStations();
        foreach (var entry in Roster)
        {
            var station = stations.FirstOrDefault(s =>
                string.Equals(s.Callsign, entry.Callsign, StringComparison.OrdinalIgnoreCase));
            if (station is null) continue;

            entry.LastHeardUtc = station.LastHeardUtc;
            if (station.Latitude.HasValue)  entry.Latitude  = station.Latitude;
            if (station.Longitude.HasValue) entry.Longitude = station.Longitude;
            if (!string.IsNullOrWhiteSpace(station.Comment)) entry.Comment = station.Comment!;
        }
        RefreshSummary();
        StatusText = $"Refreshed at {DateTimeOffset.Now.ToLocalTime():HH:mm:ss}";
    }

    // ── Private methods ───────────────────────────────────────────────

    private void AddCallsign()
    {
        var cs = NewCallsign.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cs)) return;
        if (Roster.Any(e => string.Equals(e.Callsign, cs, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"{cs} is already in the roster.";
            return;
        }

        // Try to get current data from the station database.
        var station = stationDatabase.GetStation(cs);
        AddToRoster(cs,
            station?.Latitude, station?.Longitude,
            station?.Comment ?? string.Empty,
            station?.LastHeardUtc ?? DateTimeOffset.UtcNow);

        NewCallsign = string.Empty;
        StatusText  = $"{cs} added to roster.";
    }

    private void AddToRoster(string callsign, double? lat, double? lon,
        string comment, DateTimeOffset lastHeard)
    {
        var entry = new NetControlRosterEntry
        {
            Callsign      = callsign.ToUpperInvariant(),
            LastHeardUtc  = lastHeard,
            Latitude      = lat,
            Longitude     = lon,
            Comment       = comment
        };
        Roster.Add(entry);
        ApplyFilter();
        RefreshSummary();
    }

    private void CheckInSelected()
    {
        if (selectedEntry is null) return;
        selectedEntry.Status = CheckInStatus.CheckedIn;
        StatusText = $"{selectedEntry.Callsign} checked in at {DateTimeOffset.Now.ToLocalTime():HH:mm:ss}";
        RefreshSummary();
    }

    private void StandbySelected()
    {
        if (selectedEntry is null) return;
        selectedEntry.Status = CheckInStatus.Standby;
        StatusText = $"{selectedEntry.Callsign} on standby.";
        RefreshSummary();
    }

    private void DepartSelected()
    {
        if (selectedEntry is null) return;
        selectedEntry.Status = CheckInStatus.Departed;
        StatusText = $"{selectedEntry.Callsign} departed.";
        RefreshSummary();
    }

    private void RemoveSelected()
    {
        if (selectedEntry is null) return;
        var cs = selectedEntry.Callsign;
        Roster.Remove(selectedEntry);
        FilteredRoster.Remove(selectedEntry);
        SelectedEntry = null;
        StatusText = $"{cs} removed from roster.";
        RefreshSummary();
    }

    private void ClearAll()
    {
        Roster.Clear();
        FilteredRoster.Clear();
        SelectedEntry = null;
        StatusText = "Roster cleared.";
        RefreshSummary();
    }

    private void ApplyFilter()
    {
        FilteredRoster.Clear();
        var filter = filterText.Trim();
        foreach (var entry in Roster)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                entry.Callsign.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                entry.TacticalLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                entry.Comment.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredRoster.Add(entry);
            }
        }
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CheckedInCount));
        OnPropertyChanged(nameof(StandbyCount));
        OnPropertyChanged(nameof(DepartedCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    public static NetControlViewModel CreateDesignTime()
    {
        var vm = new NetControlViewModel(new StationDatabase());
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            ("KE4CON",  CheckInStatus.CheckedIn,  now - TimeSpan.FromMinutes(2),  33.7490, -84.3880, "Net control"),
            ("W4ABC",   CheckInStatus.CheckedIn,  now - TimeSpan.FromMinutes(8),  33.7600, -84.4000, "Mobile"),
            ("N4XYZ",   CheckInStatus.Standby,    now - TimeSpan.FromMinutes(15), 33.7200, -84.3600, "Portable"),
            ("KD4DEF",  CheckInStatus.NotCheckedIn, now - TimeSpan.FromMinutes(45), 33.7800, -84.4200, "Home"),
            ("WB4GHI",  CheckInStatus.Departed,   now - TimeSpan.FromHours(1),    33.7100, -84.3500, ""),
        };
        foreach (var (cs, status, heard, lat, lon, comment) in entries)
        {
            var entry = new NetControlRosterEntry
            {
                Callsign     = cs,
                LastHeardUtc = heard,
                Latitude     = lat,
                Longitude    = lon,
                Comment      = comment
            };
            entry.Status = status;
            vm.Roster.Add(entry);
            vm.FilteredRoster.Add(entry);
        }
        vm.RefreshSummary();
        return vm;
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));


    private void SetResourceStatus(ResourceStatus status)
    {
        var selected = SelectedEntry;
        if (selected is null) { StatusText = "Select a station first."; return; }
        selected.ResourceStatus = status;
        StatusText = $"{selected.DisplayName} → {selected.ResourceStatusLabel}";
    }
}