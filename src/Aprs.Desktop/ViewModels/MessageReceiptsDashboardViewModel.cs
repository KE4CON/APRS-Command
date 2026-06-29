using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Message read receipts dashboard — a visual overview of message delivery
/// status across the session. Shows counts by status (sent, delivered,
/// pending, failed) and a sortable/filterable list of all messages with
/// their current state.
/// </summary>
public sealed class MessageReceiptsDashboardViewModel : INotifyPropertyChanged
{
    private readonly IAprsMessageStoreService messageStore;
    private string statusFilter = "All";
    private string statusText   = string.Empty;

    public MessageReceiptsDashboardViewModel(IAprsMessageStoreService messageStore)
    {
        this.messageStore = messageStore;
        Messages          = [];
        RefreshCommand    = new DesktopCommand(Refresh);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ReceiptRowViewModel> Messages { get; }

    public IReadOnlyList<string> StatusFilterOptions { get; } =
        ["All", "Acknowledged", "Sent (waiting)", "Pending/Queued", "Failed/Rejected", "Received"];

    public string StatusFilter
    {
        get => statusFilter;
        set { if (statusFilter != value) { statusFilter = value; OnPropertyChanged(); Refresh(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    // ── Summary counts ─────────────────────────────────────────────────────
    public int TotalCount        { get; private set; }
    public int AcknowledgedCount { get; private set; }
    public int WaitingCount      { get; private set; }
    public int PendingCount      { get; private set; }
    public int FailedCount       { get; private set; }
    public int ReceivedCount     { get; private set; }

    public double AcknowledgedPercent => TotalCount == 0 ? 0 : 100.0 * AcknowledgedCount / TotalCount;
    public double FailedPercent       => TotalCount == 0 ? 0 : 100.0 * FailedCount / TotalCount;

    public DesktopCommand RefreshCommand { get; }

    private void Refresh()
    {
        var all = messageStore.GetAllMessages();

        TotalCount        = all.Count;
        AcknowledgedCount = all.Count(m => m.Status == AprsMessageStatus.Acknowledged);
        WaitingCount      = all.Count(m => m.Status is AprsMessageStatus.Sent or AprsMessageStatus.WaitingForAck);
        PendingCount      = all.Count(m => m.Status is AprsMessageStatus.Pending or AprsMessageStatus.Queued
                                                       or AprsMessageStatus.RetryPending or AprsMessageStatus.Draft);
        FailedCount       = all.Count(m => m.Status is AprsMessageStatus.Failed or AprsMessageStatus.Rejected
                                                       or AprsMessageStatus.Cancelled);
        ReceivedCount     = all.Count(m => m.Status == AprsMessageStatus.Received);

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AcknowledgedCount));
        OnPropertyChanged(nameof(WaitingCount));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(ReceivedCount));
        OnPropertyChanged(nameof(AcknowledgedPercent));
        OnPropertyChanged(nameof(FailedPercent));

        var filtered = ApplyFilter(all);

        Messages.Clear();
        foreach (var m in filtered.OrderByDescending(m => m.CreatedAtUtc))
            Messages.Add(new ReceiptRowViewModel(m));

        StatusText = $"{Messages.Count} of {TotalCount} message(s) shown.";
    }

    private IEnumerable<AprsMessageRecord> ApplyFilter(IReadOnlyList<AprsMessageRecord> all) =>
        StatusFilter switch
        {
            "Acknowledged"      => all.Where(m => m.Status == AprsMessageStatus.Acknowledged),
            "Sent (waiting)"    => all.Where(m => m.Status is AprsMessageStatus.Sent or AprsMessageStatus.WaitingForAck),
            "Pending/Queued"    => all.Where(m => m.Status is AprsMessageStatus.Pending or AprsMessageStatus.Queued
                                                              or AprsMessageStatus.RetryPending or AprsMessageStatus.Draft),
            "Failed/Rejected"   => all.Where(m => m.Status is AprsMessageStatus.Failed or AprsMessageStatus.Rejected
                                                              or AprsMessageStatus.Cancelled),
            "Received"          => all.Where(m => m.Status == AprsMessageStatus.Received),
            _                   => all
        };

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ReceiptRowViewModel
{
    public ReceiptRowViewModel(AprsMessageRecord m)
    {
        Sender      = m.Sender;
        Recipient   = m.Recipient;
        Body        = m.MessageBody.Length > 50 ? m.MessageBody[..47] + "…" : m.MessageBody;
        Direction   = m.Direction == AprsMessageDirection.Outgoing ? "TX→" : "←RX";
        Timestamp   = (m.SentAtUtc ?? m.ReceivedAtUtc ?? m.CreatedAtUtc).ToLocalTime().ToString("MM/dd HH:mm:ss");
        StatusLabel = FormatStatus(m.Status);
        StatusColor = ColorForStatus(m.Status);
        StatusIcon  = IconForStatus(m.Status);
    }

    public string Sender      { get; }
    public string Recipient   { get; }
    public string Body        { get; }
    public string Direction   { get; }
    public string Timestamp   { get; }
    public string StatusLabel { get; }
    public string StatusColor { get; }
    public string StatusIcon  { get; }

    private static string FormatStatus(AprsMessageStatus s) => s switch
    {
        AprsMessageStatus.Acknowledged  => "Acknowledged",
        AprsMessageStatus.Sent          => "Sent",
        AprsMessageStatus.WaitingForAck => "Waiting for ACK",
        AprsMessageStatus.Pending       => "Pending",
        AprsMessageStatus.Queued        => "Queued",
        AprsMessageStatus.RetryPending  => "Retrying",
        AprsMessageStatus.Draft         => "Draft",
        AprsMessageStatus.Failed        => "Failed",
        AprsMessageStatus.Rejected      => "Rejected",
        AprsMessageStatus.Cancelled     => "Cancelled",
        AprsMessageStatus.Received      => "Received",
        _                               => s.ToString()
    };

    private static string ColorForStatus(AprsMessageStatus s) => s switch
    {
        AprsMessageStatus.Acknowledged  => "#15803d", // green
        AprsMessageStatus.Received      => "#15803d", // green
        AprsMessageStatus.Sent          => "#1d4ed8", // blue
        AprsMessageStatus.WaitingForAck => "#1d4ed8", // blue
        AprsMessageStatus.RetryPending  => "#b45309", // amber
        AprsMessageStatus.Pending       => "#6b7280", // gray
        AprsMessageStatus.Queued        => "#6b7280", // gray
        AprsMessageStatus.Draft         => "#6b7280", // gray
        AprsMessageStatus.Failed        => "#dc2626", // red
        AprsMessageStatus.Rejected      => "#dc2626", // red
        AprsMessageStatus.Cancelled     => "#dc2626", // red
        _                               => "#6b7280"
    };

    private static string IconForStatus(AprsMessageStatus s) => s switch
    {
        AprsMessageStatus.Acknowledged  => "✓✓",
        AprsMessageStatus.Received      => "✓",
        AprsMessageStatus.Sent          => "✓",
        AprsMessageStatus.WaitingForAck => "…",
        AprsMessageStatus.RetryPending  => "↻",
        AprsMessageStatus.Failed        => "✗",
        AprsMessageStatus.Rejected      => "✗",
        AprsMessageStatus.Cancelled     => "✗",
        _                               => "·"
    };
}
