using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class BulletinRowViewModel
{
    public BulletinRowViewModel(AprsBulletinRecord record)
    {
        Sender = record.SenderCallsign;
        BulletinId = record.BulletinId;
        Addressee = record.Addressee;
        Text = string.IsNullOrWhiteSpace(record.BulletinText) ? "-" : record.BulletinText;
        Source = record.Source.ToString();
        Received = record.ReceivedAtUtc.ToLocalTime().ToString("g");
        Status = record.IsActive ? "Active" : "Expired";
        ValidationSummary = record.ValidationErrors.Count == 0 ? "None" : string.Join("; ", record.ValidationErrors);
    }

    public string Sender { get; }

    public string BulletinId { get; }

    public string Addressee { get; }

    public string Text { get; }

    public string Source { get; }

    public string Received { get; }

    public string Status { get; }

    public string ValidationSummary { get; }
}
