using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class MessageRowViewModel
{
    public MessageRowViewModel(AprsMessageRecord message)
    {
        Message = message;
        RemoteStation = message.RemoteStationCallsign;
        Direction = message.Direction.ToString();
        Status = message.Status.ToString();
        Kind = message.Kind.ToString();
        Body = string.IsNullOrWhiteSpace(message.MessageBody) ? "None" : message.MessageBody;
        Timestamp = FormatTimestamp(message.ReceivedAtUtc ?? message.SentAtUtc ?? message.CreatedAtUtc);
        Source = message.Source.ToString();
        ValidationSummary = message.ValidationErrors.Count == 0
            ? "None"
            : string.Join("; ", message.ValidationErrors);
    }

    public AprsMessageRecord Message { get; }

    public string RemoteStation { get; }

    public string Direction { get; }

    public string Status { get; }

    public string Kind { get; }

    public string Body { get; }

    public string Timestamp { get; }

    public string Source { get; }

    public string ValidationSummary { get; }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToString("yyyy-MM-dd HH:mm 'UTC'");
    }
}
