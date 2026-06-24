namespace Aprs.Services;

public sealed record AprsMessageRecord(
    Guid Id,
    string? MessageId,
    string LocalStationCallsign,
    string RemoteStationCallsign,
    string Addressee,
    string Sender,
    string Recipient,
    string MessageBody,
    string? RawPacket,
    AprsMessageDirection Direction,
    AprsMessageStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ReceivedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    AprsPacketSource Source,
    AprsMessageKind Kind,
    IReadOnlyList<string> ValidationErrors)
{
    public AprsMessageDeliveryState DeliveryState { get; init; } = Status switch
    {
        AprsMessageStatus.Queued => AprsMessageDeliveryState.Queued,
        AprsMessageStatus.Sent => AprsMessageDeliveryState.Sent,
        AprsMessageStatus.WaitingForAck => AprsMessageDeliveryState.WaitingForAck,
        AprsMessageStatus.RetryPending => AprsMessageDeliveryState.RetryPending,
        AprsMessageStatus.Acknowledged => AprsMessageDeliveryState.Acknowledged,
        AprsMessageStatus.Rejected => AprsMessageDeliveryState.Rejected,
        AprsMessageStatus.Failed => AprsMessageDeliveryState.Failed,
        AprsMessageStatus.Cancelled => AprsMessageDeliveryState.Cancelled,
        _ => AprsMessageDeliveryState.Pending
    };

    public string? GeneratedPacket { get; init; }

    public int RetryCount { get; init; }

    public int MaximumRetries { get; init; } = 3;

    public DateTimeOffset? FirstSentAtUtc { get; init; }

    public DateTimeOffset? LastSentAtUtc { get; init; }

    public DateTimeOffset? NextRetryAtUtc { get; init; }

    public DateTimeOffset? AcknowledgedAtUtc { get; init; }

    public DateTimeOffset? FailedAtUtc { get; init; }

    public string? FailureReason { get; init; }
}
