using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aprs.Desktop.ViewModels;

public sealed class ScheduledMessagesViewModel : INotifyPropertyChanged
{
    private readonly Runtime.ScheduledMessageService service;

    private string recipient    = string.Empty;
    private string body         = string.Empty;
    private string sendDate     = string.Empty;
    private string sendTime     = string.Empty;
    private string statusText   = string.Empty;
    private ScheduledMessageRowViewModel? selectedMessage;

    public ScheduledMessagesViewModel(Runtime.ScheduledMessageService service)
    {
        this.service = service;
        Messages     = [];
        ScheduleCommand = new DesktopCommand(Schedule);
        CancelCommand   = new DesktopCommand(CancelSelected, () => SelectedMessage?.IsSent == false);

        // Pre-fill with +1 hour
        var when = DateTime.Now.AddHours(1);
        SendDate = when.ToString("MM/dd/yyyy");
        SendTime = when.ToString("HH:mm");

        service.MessageAdded   += (_, _) => RefreshList();
        service.MessageRemoved += (_, _) => RefreshList();
        service.MessageFired   += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshList);

        RefreshList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ScheduledMessageRowViewModel> Messages { get; }

    public ScheduledMessageRowViewModel? SelectedMessage
    {
        get => selectedMessage;
        set { if (selectedMessage != value) { selectedMessage = value; OnPropertyChanged(); } }
    }

    public string Recipient
    {
        get => recipient;
        set { if (recipient != value) { recipient = value; OnPropertyChanged(); } }
    }

    public string Body
    {
        get => body;
        set { if (body != value) { body = value; OnPropertyChanged(); OnPropertyChanged(nameof(CharCount)); } }
    }

    public string SendDate
    {
        get => sendDate;
        set { if (sendDate != value) { sendDate = value; OnPropertyChanged(); } }
    }

    public string SendTime
    {
        get => sendTime;
        set { if (sendTime != value) { sendTime = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public int CharCount => Body.Length;

    public DesktopCommand ScheduleCommand { get; }
    public DesktopCommand CancelCommand   { get; }

    private void Schedule()
    {
        var rcpt = Recipient.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(rcpt)) { StatusText = "Enter a recipient callsign."; return; }

        var msg = Body.Trim();
        if (string.IsNullOrWhiteSpace(msg)) { StatusText = "Enter a message."; return; }
        if (msg.Length > 67) { StatusText = $"Message too long ({msg.Length}/67 chars)."; return; }

        var dateStr = $"{SendDate.Trim()} {SendTime.Trim()}";
        if (!DateTime.TryParse(dateStr, out var localDt))
        { StatusText = "Invalid date/time. Use MM/dd/yyyy HH:mm format."; return; }

        if (localDt <= DateTime.Now)
        { StatusText = "Send time must be in the future."; return; }

        service.Schedule(rcpt, msg, new DateTimeOffset(localDt, TimeZoneInfo.Local.GetUtcOffset(localDt)));
        StatusText = $"Scheduled to {rcpt} at {localDt:MM/dd HH:mm}.";

        // Advance time by 1 hour for next entry
        var next = localDt.AddHours(1);
        SendDate = next.ToString("MM/dd/yyyy");
        SendTime = next.ToString("HH:mm");
    }

    private void CancelSelected()
    {
        if (SelectedMessage is null) return;
        if (service.Cancel(SelectedMessage.Id))
            StatusText = $"Cancelled scheduled message to {SelectedMessage.Recipient}.";
    }

    private void RefreshList()
    {
        Messages.Clear();
        foreach (var m in service.AllMessages)
            Messages.Add(new ScheduledMessageRowViewModel(m));
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ScheduledMessageRowViewModel
{
    public ScheduledMessageRowViewModel(Runtime.ScheduledMessage m)
    {
        Id          = m.Id;
        Recipient   = m.Recipient;
        Body        = m.Body.Length > 40 ? m.Body[..37] + "…" : m.Body;
        SendAtLocal = m.SendAtLocal;
        StatusLabel = m.StatusLabel;
        StatusColor = m.StatusColor;
        IsSent      = m.IsSent;
    }

    public Guid   Id          { get; }
    public string Recipient   { get; }
    public string Body        { get; }
    public string SendAtLocal { get; }
    public string StatusLabel { get; }
    public string StatusColor { get; }
    public bool   IsSent      { get; }
}
