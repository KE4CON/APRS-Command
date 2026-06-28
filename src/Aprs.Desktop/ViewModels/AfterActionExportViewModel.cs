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

    private string eventName = string.Empty;
    private bool includeStations = true;
    private bool includeMessages = true;
    private bool includePacketLog = false;
    private bool includeTextSummary = true;
    private string statusText = string.Empty;
    private bool isExporting;
    private DateTimeOffset sessionStart;

    public AfterActionExportViewModel(
        IStationDatabase stationDatabase,
        IAprsMessageStoreService messageStore,
        IRawPacketLogService packetLog,
        IAppSettingsStore settingsStore)
    {
        this.stationDatabase  = stationDatabase;
        this.messageStore     = messageStore;
        this.packetLog        = packetLog;
        this.settingsStore    = settingsStore;
        sessionStart          = DateTimeOffset.UtcNow;

        ExportCommand = new DesktopCommand(async () => await ExportAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<(string FileName, string Content)>? SaveFileRequested;

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
            rt.GetService<IAppSettingsStore>());
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
