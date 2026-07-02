using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;
using Avalonia.Threading;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// ViewModel for the packet statistics dashboard.
/// Refreshes from PacketStatisticsService on a timer.
/// </summary>
public sealed class PacketStatisticsDashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PacketStatisticsService stats;
    private readonly DispatcherTimer refreshTimer;

    public PacketStatisticsDashboardViewModel(PacketStatisticsService stats)
    {
        this.stats = stats;
        refreshTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
            (_, _) => Refresh());
        refreshTimer.Start();
        Refresh();
    }

    // ── Session ──────────────────────────────────────────────────────────────
    private string sessionDuration = "0:00:00";
    public string SessionDuration
    {
        get => sessionDuration;
        private set { sessionDuration = value; OnPropertyChanged(); }
    }

    private string sessionStart = string.Empty;
    public string SessionStart
    {
        get => sessionStart;
        private set { sessionStart = value; OnPropertyChanged(); }
    }

    // ── Totals ───────────────────────────────────────────────────────────────
    private int totalPackets;
    public int TotalPackets
    {
        get => totalPackets;
        private set { totalPackets = value; OnPropertyChanged(); }
    }

    private int uniqueStations;
    public int UniqueStations
    {
        get => uniqueStations;
        private set { uniqueStations = value; OnPropertyChanged(); }
    }

    private string packetsPerHour = "0";
    public string PacketsPerHour
    {
        get => packetsPerHour;
        private set { packetsPerHour = value; OnPropertyChanged(); }
    }

    private string packetsPerMinute = "0";
    public string PacketsPerMinute
    {
        get => packetsPerMinute;
        private set { packetsPerMinute = value; OnPropertyChanged(); }
    }

    // ── Packet types ─────────────────────────────────────────────────────────
    private int positionPackets;
    public int PositionPackets
    {
        get => positionPackets;
        private set { positionPackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionPct)); }
    }

    private int messagePackets;
    public int MessagePackets
    {
        get => messagePackets;
        private set { messagePackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(MessagePct)); }
    }

    private int weatherPackets;
    public int WeatherPackets
    {
        get => weatherPackets;
        private set { weatherPackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(WeatherPct)); }
    }

    private int objectPackets;
    public int ObjectPackets
    {
        get => objectPackets;
        private set { objectPackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(ObjectPct)); }
    }

    private int statusPackets;
    public int StatusPackets
    {
        get => statusPackets;
        private set { statusPackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusPct)); }
    }

    private int otherPackets;
    public int OtherPackets
    {
        get => otherPackets;
        private set { otherPackets = value; OnPropertyChanged(); OnPropertyChanged(nameof(OtherPct)); }
    }

    private int invalidPackets;
    public int InvalidPackets
    {
        get => invalidPackets;
        private set { invalidPackets = value; OnPropertyChanged(); }
    }

    // Percentages for progress-bar display
    public double PositionPct => totalPackets > 0 ? (double)positionPackets / totalPackets * 100 : 0;
    public double MessagePct  => totalPackets > 0 ? (double)messagePackets  / totalPackets * 100 : 0;
    public double WeatherPct  => totalPackets > 0 ? (double)weatherPackets  / totalPackets * 100 : 0;
    public double ObjectPct   => totalPackets > 0 ? (double)objectPackets   / totalPackets * 100 : 0;
    public double StatusPct   => totalPackets > 0 ? (double)statusPackets   / totalPackets * 100 : 0;
    public double OtherPct    => totalPackets > 0 ? (double)otherPackets    / totalPackets * 100 : 0;

    // ── Top stations ─────────────────────────────────────────────────────────
    private IReadOnlyList<TopStationEntry> topStations = [];
    public IReadOnlyList<TopStationEntry> TopStations
    {
        get => topStations;
        private set { topStations = value; OnPropertyChanged(); }
    }

    // ── Hourly activity chart data ────────────────────────────────────────────
    private IReadOnlyList<HourlyBucket> hourlyData = [];
    public IReadOnlyList<HourlyBucket> HourlyData
    {
        get => hourlyData;
        private set { hourlyData = value; OnPropertyChanged(); }
    }

    private int peakHourlyCount;
    public int PeakHourlyCount
    {
        get => peakHourlyCount;
        private set { peakHourlyCount = value; OnPropertyChanged(); }
    }

    private string peakActivityLabel = "—";
    public string PeakActivityLabel
    {
        get => peakActivityLabel;
        private set { peakActivityLabel = value; OnPropertyChanged(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public void ResetStats()
    {
        stats.Reset();
        Refresh();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────
    private void Refresh()
    {
        var dur = stats.SessionDuration;
        SessionDuration = $"{(int)dur.TotalHours}:{dur.Minutes:D2}:{dur.Seconds:D2}";
        SessionStart    = stats.SessionStartUtc.ToLocalTime().ToString("MMM d, yyyy  h:mm tt");

        TotalPackets    = stats.TotalPackets;
        UniqueStations  = stats.UniqueStations;
        PacketsPerHour  = $"{stats.PacketsPerHour:F1}";
        PacketsPerMinute = $"{stats.PacketsPerMinute:F2}";

        PositionPackets = stats.PositionPackets;
        MessagePackets  = stats.MessagePackets;
        WeatherPackets  = stats.WeatherPackets;
        ObjectPackets   = stats.ObjectPackets;
        StatusPackets   = stats.StatusPackets;
        OtherPackets    = stats.OtherPackets;
        InvalidPackets  = stats.InvalidPackets;

        // Top stations
        var top = stats.GetTopStations(15);
        var maxCount = top.Count > 0 ? top[0].Count : 1;
        TopStations = top
            .Select(t => new TopStationEntry(t.Callsign, t.Count, (double)t.Count / maxCount * 100))
            .ToList();

        // Hourly buckets
        var buckets = stats.GetHourlyBuckets();
        var nowHour = DateTimeOffset.UtcNow.ToLocalTime().Hour;
        var peak = buckets.Max();
        PeakHourlyCount = peak;

        var hourlyList = new List<HourlyBucket>();
        for (int i = 0; i < 24; i++)
        {
            var hour = (nowHour - 23 + i + 24) % 24;
            var label = hour == 0 ? "12a" : hour < 12 ? $"{hour}a" : hour == 12 ? "12p" : $"{hour - 12}p";
            var barHeight = peak > 0 ? (double)buckets[i] / peak * 100 : 0;
            hourlyList.Add(new HourlyBucket(label, buckets[i], barHeight));
        }
        HourlyData = hourlyList;

        // Peak activity
        if (peak > 0)
        {
            var peakIdx = buckets.ToList().IndexOf(peak);
            var peakHour = (nowHour - 23 + peakIdx + 24) % 24;
            var ampm = peakHour < 12 ? "AM" : "PM";
            var h12 = peakHour == 0 ? 12 : peakHour > 12 ? peakHour - 12 : peakHour;
            PeakActivityLabel = $"{h12}:00 {ampm}  ({peak} packets)";
        }
        else
        {
            PeakActivityLabel = "No activity yet";
        }
    }

    public void Dispose()
    {
        refreshTimer.Stop();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record TopStationEntry(string Callsign, int Count, double BarWidthPct);
public sealed record HourlyBucket(string HourLabel, int Count, double BarHeightPct);
