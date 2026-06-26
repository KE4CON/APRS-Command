using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Core;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class RfDiagnosticsViewModel : INotifyPropertyChanged
{
    private readonly IRfDiagnosticsService diagnosticsService;

    public RfDiagnosticsViewModel(IRfDiagnosticsService diagnosticsService)
    {
        this.diagnosticsService = diagnosticsService;
        RecentPackets = new ObservableCollection<RfDiagnosticPacketRowViewModel>();
        StationRates = new ObservableCollection<string>();
        SourceRates = new ObservableCollection<string>();
        PathWarnings = new ObservableCollection<string>();
        ExcessiveBeaconWarnings = new ObservableCollection<string>();
        ClearCommand = new DesktopCommand(Clear);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RfDiagnosticPacketRowViewModel> RecentPackets { get; }

    public ObservableCollection<string> StationRates { get; }

    public ObservableCollection<string> SourceRates { get; }

    public ObservableCollection<string> PathWarnings { get; }

    public ObservableCollection<string> ExcessiveBeaconWarnings { get; }

    /// <summary>Raw text output from Direwolf or other modem processes.</summary>
    public ObservableCollection<string> ConsoleOutput { get; } = new();

    /// <summary>Adds a line of text from Direwolf or the managed modem process.</summary>
    public void AddConsoleOutput(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        ConsoleOutput.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {line}");
        // Keep the last 500 lines to avoid unbounded growth.
        while (ConsoleOutput.Count > 500)
            ConsoleOutput.RemoveAt(0);
    }

    public DesktopCommand ClearCommand { get; }

    public string TotalPacketsText { get; private set; } = "0";

    public string RfPacketsText { get; private set; } = "0";

    public string AprsIsPacketsText { get; private set; } = "0";

    public string DuplicatePacketsText { get; private set; } = "0";

    public string UniqueStationsText { get; private set; } = "0";

    public string LinkSummaryText { get; private set; } = "RF-only 0, APRS-IS-only 0, both 0";

    public string LastUpdatedText { get; private set; } = "-";

    /// <summary>Feeds a live RF packet into the diagnostics service and refreshes the display.</summary>
    public void AcceptPacket(Aprs.Core.AprsPacket packet, AprsPacketSource source)
    {
        diagnosticsService.AcceptPacket(packet, source, source.ToString());
        Refresh();
    }

    public void Refresh()
    {
        var summary = diagnosticsService.GetSummary();
        TotalPacketsText = summary.TotalPacketsAnalyzed.ToString();
        RfPacketsText = summary.RfPackets.ToString();
        AprsIsPacketsText = summary.AprsIsPackets.ToString();
        DuplicatePacketsText = summary.DuplicatePackets.ToString();
        UniqueStationsText = summary.UniqueStations.ToString();
        LinkSummaryText = $"RF-only {summary.RfOnlyPacketCount}, APRS-IS-only {summary.AprsIsOnlyPacketCount}, both {summary.SeenOnBothRfAndAprsIsPacketCount}";
        LastUpdatedText = summary.LastUpdatedTimestampUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

        Replace(RecentPackets, diagnosticsService.GetRecentPackets(50).Select(packet => new RfDiagnosticPacketRowViewModel(packet)));
        Replace(StationRates, diagnosticsService.GetPacketRateByCallsign().OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key}: {pair.Value}"));
        Replace(SourceRates, diagnosticsService.GetPacketRateBySourcePort().OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key}: {pair.Value}"));
        Replace(PathWarnings, summary.PathWarnings.DefaultIfEmpty("No path warnings."));
        Replace(ExcessiveBeaconWarnings, summary.ExcessiveBeaconWarnings.DefaultIfEmpty("No excessive beacon warnings."));

        OnPropertyChanged(nameof(TotalPacketsText));
        OnPropertyChanged(nameof(RfPacketsText));
        OnPropertyChanged(nameof(AprsIsPacketsText));
        OnPropertyChanged(nameof(DuplicatePacketsText));
        OnPropertyChanged(nameof(UniqueStationsText));
        OnPropertyChanged(nameof(LinkSummaryText));
        OnPropertyChanged(nameof(LastUpdatedText));
    }

    public static RfDiagnosticsViewModel CreateDesignTime()
    {
        var parser = new AprsParser();
        var service = new RfDiagnosticsService(new RfDiagnosticsConfiguration(
            DiagnosticsEnabled: true,
            DuplicateDetectionWindow: TimeSpan.FromMinutes(5),
            PacketRateWindow: TimeSpan.FromMinutes(10),
            MinimumBeaconInterval: TimeSpan.FromSeconds(30),
            MaximumRecentPackets: 100,
            ExcessiveBeaconCountThreshold: 3,
            ExcessivePortPacketCountThreshold: 10,
            MaximumRecommendedPathComponents: 4,
            Notes: "Design-time RF diagnostics sample."));
        var now = DateTimeOffset.UtcNow;

        service.AcceptPacket(Parse(parser, "MOBILE1>APRS,WIDE1-1,WIDE2-1:!3903.50N/08430.50W>Mobile", now), AprsPacketSource.Rf, "RF", now);
        service.AcceptPacket(Parse(parser, "MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile", now.AddSeconds(10)), AprsPacketSource.AprsIs, "APRS-IS", now.AddSeconds(10));
        service.AcceptPacket(Parse(parser, "DIGI1>APRS,WIDE1-1*,WIDE2-1:>Digi heard", now.AddSeconds(20)), AprsPacketSource.TcpKiss, "Direwolf", now.AddSeconds(20));

        return new RfDiagnosticsViewModel(service);
    }

    private void Clear()
    {
        diagnosticsService.ClearDiagnostics();
        Refresh();
    }

    private static AprsPacket Parse(AprsParser parser, string rawPacket, DateTimeOffset timestamp)
    {
        parser.TryParse(rawPacket, timestamp, out var packet, out _);
        return packet!;
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
