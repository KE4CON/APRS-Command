using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Message broadcast — send one APRS message to multiple stations simultaneously.
/// Useful for net control announcements, exercise traffic, or notifications
/// that need to reach every station in the roster.
/// </summary>
public sealed class MessageBroadcastViewModel : INotifyPropertyChanged
{
    private readonly MessageCenterViewModel messageCenter;
    private readonly string localCallsign;

    private string recipientList = string.Empty;
    private string messageBody   = string.Empty;
    private string statusText    = string.Empty;
    private bool isSending;

    public MessageBroadcastViewModel(MessageCenterViewModel messageCenter, string localCallsign)
    {
        this.messageCenter  = messageCenter;
        this.localCallsign  = localCallsign;
        Results             = [];
        SendCommand         = new DesktopCommand(async () => await SendAsync());
        ClearCommand        = new DesktopCommand(Clear);
        AddFromRosterCommand = new DesktopCommand(AddFromRoster);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public Func<IReadOnlyList<string>>? RequestRosterCallsigns { get; set; }

    public ObservableCollection<BroadcastResultViewModel> Results { get; }

    public string RecipientList
    {
        get => recipientList;
        set { if (recipientList != value) { recipientList = value; OnPropertyChanged(); OnPropertyChanged(nameof(RecipientCount)); } }
    }

    public string MessageBody
    {
        get => messageBody;
        set { if (messageBody != value) { messageBody = value; OnPropertyChanged(); OnPropertyChanged(nameof(CharCount)); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsSending
    {
        get => isSending;
        private set { if (isSending != value) { isSending = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotSending)); } }
    }

    public bool IsNotSending => !isSending;

    public int RecipientCount => ParseRecipients().Count;
    public int CharCount      => MessageBody.Length;

    public DesktopCommand SendCommand         { get; }
    public DesktopCommand ClearCommand        { get; }
    public DesktopCommand AddFromRosterCommand { get; }

    private void AddFromRoster()
    {
        var callsigns = RequestRosterCallsigns?.Invoke() ?? [];
        if (callsigns.Count == 0) { StatusText = "No stations in net control roster."; return; }

        var existing = ParseRecipients().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd    = callsigns.Where(c => !existing.Contains(c)).ToList();

        if (toAdd.Count == 0) { StatusText = "All roster stations already in recipient list."; return; }

        var combined = ParseRecipients().Concat(toAdd).ToList();
        RecipientList = string.Join(", ", combined);
        StatusText = $"Added {toAdd.Count} station(s) from roster.";
    }

    private async Task SendAsync()
    {
        var recipients = ParseRecipients();
        var body       = MessageBody.Trim();

        if (recipients.Count == 0) { StatusText = "Enter at least one recipient callsign."; return; }
        if (string.IsNullOrWhiteSpace(body)) { StatusText = "Enter a message."; return; }
        if (body.Length > 67) { StatusText = $"Message too long ({body.Length}/67 chars). APRS messages are limited to 67 characters."; return; }

        IsSending  = true;
        StatusText = $"Sending to {recipients.Count} station(s)…";
        Results.Clear();

        int sent = 0, failed = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                var record = await messageCenter.SendMessageAsync(localCallsign, recipient, body)
                                               .ConfigureAwait(false);
                var success = record is not null;
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Results.Add(new BroadcastResultViewModel(recipient, success,
                        success ? "Queued" : "Failed"));
                    if (success) sent++; else failed++;
                });
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Results.Add(new BroadcastResultViewModel(recipient, false, ex.Message));
                    failed++;
                });
            }

            // Small delay between sends to avoid flooding
            await Task.Delay(200).ConfigureAwait(false);
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsSending  = false;
            StatusText = failed == 0
                ? $"✓ Sent to all {sent} station(s)."
                : $"Sent: {sent}, Failed: {failed}.";
        });
    }

    private void Clear()
    {
        Results.Clear();
        StatusText = string.Empty;
    }

    private List<string> ParseRecipients() =>
        RecipientList
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class BroadcastResultViewModel
{
    public BroadcastResultViewModel(string callsign, bool success, string detail)
    {
        Callsign = callsign;
        Success  = success;
        Detail   = detail;
        Icon     = success ? "✓" : "✗";
        Color    = success ? "#15803d" : "#dc2626";
    }

    public string Callsign { get; }
    public bool   Success  { get; }
    public string Detail   { get; }
    public string Icon     { get; }
    public string Color    { get; }
}
