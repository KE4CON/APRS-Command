using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Drives the after-action report export window. Operator enters event details,
/// chooses what to include, and exports to one or more CSV files plus a text summary.
/// </summary>
public sealed class AfterActionExportViewModel : INotifyPropertyChanged
{
    private readonly IStationDatabase stationDatabase;
    private readonly IAprsMessageStoreService messageStore;
    private readonly IRawPacketLogService packetLog;
    private readonly IAppSettingsStore settingsStore;
    private NetControlViewModel? netControlRoster;

    private string eventName = string.Empty;
    private bool includeStations = true;
    private bool includeMessages = true;
    private bool includePacketLog = false;
    private bool includeTextSummary = true;
    private string statusText = string.Empty;
    private bool isExporting;
    private bool includeIcs214 = true;
    private bool includeIcs205 = true;
    private bool includeIcs211 = true;
    private bool includeIcs213 = false; // off by default — generates one form per message
    private string checkInLocation = "Incident Command Post";
    private string specialInstructions = string.Empty;
    private string operatorName = string.Empty;
    private string icsPosition = "Communications Unit Leader";
    private string homeAgency = string.Empty;
    private DateTimeOffset sessionStart;

    public AfterActionExportViewModel(
        IStationDatabase stationDatabase,
        IAprsMessageStoreService messageStore,
        IRawPacketLogService packetLog,
        IAppSettingsStore settingsStore,
        NetControlViewModel? netControlRoster = null)
    {
        this.stationDatabase  = stationDatabase;
        this.messageStore     = messageStore;
        this.packetLog        = packetLog;
        this.settingsStore    = settingsStore;
        sessionStart          = DateTimeOffset.UtcNow;
        this.netControlRoster = netControlRoster;

        ExportCommand = new DesktopCommand(async () => await ExportAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<(string FileName, string Content)>? SaveFileRequested;

    public string OperatorName
    {
        get => operatorName;
        set { if (operatorName != value) { operatorName = value; OnPropertyChanged(); } }
    }

    public string IcsPosition
    {
        get => icsPosition;
        set { if (icsPosition != value) { icsPosition = value; OnPropertyChanged(); } }
    }

    public string HomeAgency
    {
        get => homeAgency;
        set { if (homeAgency != value) { homeAgency = value; OnPropertyChanged(); } }
    }

    public string EventName
    {
        get => eventName;
        set { if (eventName != value) { eventName = value; OnPropertyChanged(); } }
    }

    public bool IncludeStations
    {
        get => includeStations;
        set { if (includeStations != value) { includeStations = value; OnPropertyChanged(); } }
    }

    public bool IncludeMessages
    {
        get => includeMessages;
        set { if (includeMessages != value) { includeMessages = value; OnPropertyChanged(); } }
    }

    public bool IncludePacketLog
    {
        get => includePacketLog;
        set { if (includePacketLog != value) { includePacketLog = value; OnPropertyChanged(); } }
    }

    public bool IncludeIcs213
    {
        get => includeIcs213;
        set { if (includeIcs213 != value) { includeIcs213 = value; OnPropertyChanged(); } }
    }

    public bool IncludeIcs211
    {
        get => includeIcs211;
        set { if (includeIcs211 != value) { includeIcs211 = value; OnPropertyChanged(); } }
    }

    public string CheckInLocation
    {
        get => checkInLocation;
        set { if (checkInLocation != value) { checkInLocation = value; OnPropertyChanged(); } }
    }

    public bool IncludeIcs205
    {
        get => includeIcs205;
        set { if (includeIcs205 != value) { includeIcs205 = value; OnPropertyChanged(); } }
    }

    public string SpecialInstructions
    {
        get => specialInstructions;
        set { if (specialInstructions != value) { specialInstructions = value; OnPropertyChanged(); } }
    }

    public bool IncludeIcs214
    {
        get => includeIcs214;
        set { if (includeIcs214 != value) { includeIcs214 = value; OnPropertyChanged(); } }
    }

    public bool IncludeTextSummary
    {
        get => includeTextSummary;
        set { if (includeTextSummary != value) { includeTextSummary = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsExporting
    {
        get => isExporting;
        private set { if (isExporting != value) { isExporting = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotExporting)); } }
    }

    public bool IsNotExporting => !isExporting;

    public DesktopCommand ExportCommand { get; }

    /// <summary>
    /// Summary counts shown in the window so the operator knows what will be exported.
    /// </summary>
    public int StationCount   => stationDatabase.GetAllStations().Count;
    public int MessageCount   => messageStore.GetAllMessages().Count;
    public int PacketCount    => packetLog.GetRecentEntries().Count;

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(StationCount));
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(PacketCount));
    }

    private async Task ExportAsync()
    {
        if (IsExporting) return;
        IsExporting = true;
        StatusText = "Preparing export…";

        try
        {
            var reportTime  = DateTimeOffset.UtcNow;
            var callsign    = settingsStore.Load().Station.Callsign;
            var name        = string.IsNullOrWhiteSpace(EventName) ? "APRS Session" : EventName.Trim();
            var dateStamp   = reportTime.ToLocalTime().ToString("yyyy-MM-dd_HHmm");
            var filesExported = 0;

            if (IncludeTextSummary)
            {
                var stations = stationDatabase.GetAllStations();
                var messages = messageStore.GetAllMessages();
                var packets  = packetLog.GetRecentEntries();
                var text     = AfterActionReportService.GenerateTextSummary(
                    callsign, name, sessionStart, reportTime, stations, messages, packets);
                SaveFileRequested?.Invoke(this, ($"AAR_{dateStamp}_Summary.txt", text));
                filesExported++;
                StatusText = "Generating text summary…";
                await Task.Delay(50);
            }

            if (IncludeStations)
            {
                var stations = stationDatabase.GetAllStations();
                var csv      = AfterActionReportService.GenerateStationsCsv(stations, reportTime);
                SaveFileRequested?.Invoke(this, ($"AAR_{dateStamp}_Stations.csv", csv));
                filesExported++;
                StatusText = "Generating stations CSV…";
                await Task.Delay(50);
            }

            if (IncludeMessages)
            {
                var messages = messageStore.GetAllMessages();
                var csv      = AfterActionReportService.GenerateMessagesCsv(messages, reportTime);
                SaveFileRequested?.Invoke(this, ($"AAR_{dateStamp}_Messages.csv", csv));
                filesExported++;
                StatusText = "Generating messages CSV…";
                await Task.Delay(50);
            }

            if (IncludePacketLog)
            {
                var packets = packetLog.GetRecentEntries();
                var csv     = AfterActionReportService.GeneratePacketLogCsv(packets, reportTime);
                SaveFileRequested?.Invoke(this, ($"AAR_{dateStamp}_PacketLog.csv", csv));
                filesExported++;
            }

            if (IncludeIcs213)
            {
                StatusText = "Generating ICS-213 message forms…";
                await Task.Delay(50);
                var messages = messageStore.GetAllMessages();
                var opName = string.IsNullOrWhiteSpace(OperatorName) ? callsign : OperatorName;
                var ics213Messages = messages.Select(m => new Ics213Message(
                    m.Sender, m.Recipient, m.MessageBody,
                    m.Direction == AprsMessageDirection.Outgoing,
                    m.CreatedAtUtc, m.SentAtUtc, m.ReceivedAtUtc)).ToList();
                var ics213 = Ics213ExportService.GenerateIcs213Bundle(
                    operatorCallsign: callsign,
                    operatorName:     opName,
                    incidentName:     name,
                    messages:         ics213Messages);
                SaveFileRequested?.Invoke(this, ($"ICS213_{dateStamp}.txt", ics213));
                filesExported++;
            }

            if (IncludeIcs211)
            {
                StatusText = "Generating ICS-211…";
                await Task.Delay(50);
                var opName = string.IsNullOrWhiteSpace(OperatorName) ? callsign : OperatorName;
                // Build roster entries from the net control roster if available,
                // otherwise from the station database.
                var rosterEntries = netControlRoster?.Roster
                    .Select(e => new Ics211RosterEntry(
                        e.Callsign, e.TacticalLabel,
                        e.ResourceCategory.Length > 0 ? $"{e.ResourceTypeLabel} {e.ResourceCategory}".Trim() : e.ResourceTypeLabel,
                        e.Status switch
                        {
                            CheckInStatus.CheckedIn    => "Checked In",
                            CheckInStatus.Standby      => "Standby",
                            CheckInStatus.Departed     => "Departed",
                            _                          => "Not Checked In"
                        },
                        e.CheckedInAt, null))
                    .ToList()
                    ?? stationDatabase.GetAllStations()
                        .Select(s => new Ics211RosterEntry(
                            s.Callsign, s.TacticalLabel,
                            null, "Observed", s.LastHeardUtc, null))
                        .ToList();

                var ics211 = Ics211ExportService.GenerateIcs211(
                    incidentName:     name,
                    operatorCallsign: callsign,
                    operatorName:     opName,
                    checkInLocation:  CheckInLocation,
                    periodFrom:       sessionStart,
                    periodTo:         reportTime,
                    entries:          rosterEntries);
                SaveFileRequested?.Invoke(this, ($"ICS211_{dateStamp}.txt", ics211));
                filesExported++;
            }

            if (IncludeIcs205)
            {
                StatusText = "Generating ICS-205…";
                await Task.Delay(50);
                var frequencies = settingsStore.Load().FrequencyReference.Entries;
                var opName = string.IsNullOrWhiteSpace(OperatorName) ? callsign : OperatorName;
                var ics205 = Ics205ExportService.GenerateIcs205(
                    incidentName:        name,
                    operatorCallsign:    callsign,
                    operatorName:        opName,
                    icsPosition:         IcsPosition,
                    periodFrom:          sessionStart,
                    periodTo:            reportTime,
                    frequencies:         frequencies.ToList(),
                    specialInstructions: string.IsNullOrWhiteSpace(SpecialInstructions)
                                         ? null : SpecialInstructions);
                SaveFileRequested?.Invoke(this, ($"ICS205_{dateStamp}.txt", ics205));
                filesExported++;
            }

            if (IncludeIcs214)
            {
                StatusText = "Generating ICS-214…";
                await Task.Delay(50);
                var stations = stationDatabase.GetAllStations();
                var messages = messageStore.GetAllMessages();
                var msgSnapshots = messages.Select(m => new AprsMessageSnapshot(
                    m.CreatedAtUtc, m.Sender, m.Recipient, m.MessageBody,
                    m.Direction == AprsMessageDirection.Outgoing)).ToList();
                var opName = string.IsNullOrWhiteSpace(OperatorName) ? callsign : OperatorName;
                var ics214 = Ics214ExportService.GenerateIcs214(
                    incidentName:      name,
                    operatorName:      opName,
                    operatorCallsign:  callsign,
                    icsPosition:       IcsPosition,
                    homeAgency:        HomeAgency,
                    periodFrom:        sessionStart,
                    periodTo:          reportTime,
                    stations:          stations,
                    messages:          msgSnapshots);
                SaveFileRequested?.Invoke(this, ($"ICS214_{dateStamp}.txt", ics214));
                filesExported++;
            }

            StatusText = filesExported == 0
                ? "Nothing selected — tick at least one export option."
                : $"✓ {filesExported} file{(filesExported == 1 ? "" : "s")} ready to save.";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    public static AfterActionExportViewModel CreateDesignTime()
        => new(
            new StationDatabase(),
            new AprsMessageStoreService(),
            new RawPacketLogService(),
            new InMemoryAppSettingsStore());

    public static AfterActionExportViewModel CreateFromRuntime(Aprs.Desktop.Composition.DesktopRuntime? rt)
    {
        if (rt is null) return CreateDesignTime();
        return new AfterActionExportViewModel(
            rt.GetService<IStationDatabase>(),
            rt.GetService<IAprsMessageStoreService>(),
            rt.GetService<IRawPacketLogService>(),
            rt.GetService<IAppSettingsStore>(),
            rt.MainViewModel.NetControl);
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
