using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class QueryRowViewModel
{
    public QueryRowViewModel(AprsQueryRecord record)
    {
        Sender = record.SenderCallsign;
        QueryType = record.QueryType;
        QueryBody = string.IsNullOrWhiteSpace(record.QueryBody) ? "-" : record.QueryBody;
        Source = record.Source.ToString();
        Received = record.ReceivedAtUtc.ToLocalTime().ToString("g");
        ValidationSummary = record.ValidationErrors.Count == 0 ? "None" : string.Join("; ", record.ValidationErrors);
    }

    public string Sender { get; }

    public string QueryType { get; }

    public string QueryBody { get; }

    public string Source { get; }

    public string Received { get; }

    public string ValidationSummary { get; }
}
