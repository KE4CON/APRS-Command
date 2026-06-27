using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Drives the NWS weather alerts banner and detail panel.
/// Shows active alerts for the operator's configured location,
/// refreshed every 5 minutes from api.weather.gov.
/// </summary>
public sealed class NwsAlertsViewModel : INotifyPropertyChanged
{
    private NwsAlertRecord? selectedAlert;
    private bool hasAlerts;
    private string summaryText = "No active weather alerts.";
    private string bannerColor = "#475569";
    private string bannerBg    = "#F8FAFC";
    private DateTimeOffset lastRefreshed;

    public ObservableCollection<NwsAlertRecord> Alerts { get; } = [];

    public NwsAlertRecord? SelectedAlert
    {
        get => selectedAlert;
        set { if (selectedAlert != value) { selectedAlert = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedAlert)); } }
    }

    public bool HasSelectedAlert => selectedAlert is not null;

    public bool HasAlerts
    {
        get => hasAlerts;
        private set { if (hasAlerts != value) { hasAlerts = value; OnPropertyChanged(); } }
    }

    public string SummaryText
    {
        get => summaryText;
        private set { if (summaryText != value) { summaryText = value; OnPropertyChanged(); } }
    }

    public string BannerColor
    {
        get => bannerColor;
        private set { if (bannerColor != value) { bannerColor = value; OnPropertyChanged(); } }
    }

    public string BannerBg
    {
        get => bannerBg;
        private set { if (bannerBg != value) { bannerBg = value; OnPropertyChanged(); } }
    }

    public string LastRefreshedText =>
        lastRefreshed == default
            ? "Never refreshed"
            : $"Updated {lastRefreshed.ToLocalTime():HH:mm:ss}";

    /// <summary>Called by WireNwsAlerts() whenever the service fetches new alerts.</summary>
    public void UpdateAlerts(IReadOnlyList<NwsAlertRecord> alerts)
    {
        lastRefreshed = DateTimeOffset.UtcNow;
        Alerts.Clear();
        foreach (var alert in alerts)
            Alerts.Add(alert);

        HasAlerts = alerts.Count > 0;
        SelectedAlert = alerts.Count > 0 ? alerts[0] : null;

        if (alerts.Count == 0)
        {
            SummaryText = "No active weather alerts for your location.";
            BannerColor = "#475569";
            BannerBg    = "#F8FAFC";
        }
        else
        {
            var worst = alerts[0]; // sorted by severity descending
            SummaryText = alerts.Count == 1
                ? $"⚠ {worst.Event} — {worst.AreaDescription}"
                : $"⚠ {alerts.Count} active alerts — {worst.Event} and more";
            BannerColor = worst.SeverityColor;
            BannerBg    = worst.SeverityBg;
        }

        OnPropertyChanged(nameof(LastRefreshedText));
    }

    public static NwsAlertsViewModel CreateDesignTime()
    {
        var vm = new NwsAlertsViewModel();
        vm.UpdateAlerts([
            new NwsAlertRecord(
                "1", "Severe Thunderstorm Warning",
                "Severe Thunderstorm Warning issued June 26 at 8:00PM EDT",
                "At 800 PM EDT, a severe thunderstorm was located near Atlanta...",
                "Severe", "Immediate", "Observed",
                "Northern Georgia", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(2), "NWS Atlanta GA"),
            new NwsAlertRecord(
                "2", "Flash Flood Watch",
                "Flash Flood Watch in effect through Saturday evening",
                "Heavy rainfall may produce flash flooding...",
                "Moderate", "Expected", "Likely",
                "Central Georgia", DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(12), "NWS Atlanta GA")
        ]);
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
