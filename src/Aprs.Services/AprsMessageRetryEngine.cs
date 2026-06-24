using Aprs.Core;

namespace Aprs.Services;

public sealed class AprsMessageRetryEngine : IAprsMessageRetryEngine
{
    private readonly IAprsMessageStoreService messageStore;
    private readonly IAprsMessageTransmitService transmitService;
    private readonly IAprsMessageIdGenerator messageIdGenerator;
    private readonly AprsMessageRetryConfiguration configuration;

    public AprsMessageRetryEngine(
        IAprsMessageStoreService messageStore,
        IAprsMessageTransmitService transmitService,
        IAprsMessageIdGenerator? messageIdGenerator = null,
        AprsMessageRetryConfiguration? configuration = null)
    {
        this.messageStore = messageStore;
        this.transmitService = transmitService;
        this.messageIdGenerator = messageIdGenerator ?? new SequentialAprsMessageIdGenerator();
        this.configuration = configuration ?? AprsMessageRetryConfiguration.Default;
    }

    public async Task<AprsMessageRecord> SendMessageAsync(Guid messageRecordId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var record = messageStore.GetAllMessages().Single(message => message.Id == messageRecordId);
        var messageId = string.IsNullOrWhiteSpace(record.MessageId) ? messageIdGenerator.NextId() : record.MessageId;
        var rawPacket = FormatMessagePacket(record, messageId);
        var transmitResult = await transmitService.SendAsync(rawPacket, cancellationToken);

        return transmitResult.IsSuccess
            ? MarkWaitingForAck(record, messageId, rawPacket, transmitResult.TimestampUtc, retryCount: record.RetryCount)
            : messageStore.UpdateDelivery(record.Id, current => current with
            {
                Direction = AprsMessageDirection.Outgoing,
                Status = AprsMessageStatus.Failed,
                DeliveryState = AprsMessageDeliveryState.Failed,
                MessageId = messageId,
                GeneratedPacket = rawPacket,
                LastUpdatedAtUtc = transmitResult.TimestampUtc,
                FailedAtUtc = transmitResult.TimestampUtc,
                FailureReason = transmitResult.FailureReason ?? "Message transmit failed.",
                ValidationErrors = [.. current.ValidationErrors, transmitResult.FailureReason ?? "Message transmit failed."]
            });
    }

    public AprsMessageRecord? ProcessAckOrRej(MessageAprsPacket packet, DateTimeOffset now)
    {
        var ackId = packet.AcknowledgedMessageId;
        var rejId = packet.RejectedMessageId;
        if (string.IsNullOrWhiteSpace(ackId) && string.IsNullOrWhiteSpace(rejId))
        {
            return null;
        }

        var matchId = ackId ?? rejId!;
        var matching = messageStore.GetOutboxMessages()
            .Concat(messageStore.GetAllMessages().Where(message => message.Direction == AprsMessageDirection.Outgoing))
            .DistinctBy(message => message.Id)
            .FirstOrDefault(message => string.Equals(message.MessageId, matchId, StringComparison.OrdinalIgnoreCase));

        if (matching is null)
        {
            return null;
        }

        return ackId is not null
            ? messageStore.UpdateDelivery(matching.Id, record => record with
            {
                Status = AprsMessageStatus.Acknowledged,
                DeliveryState = AprsMessageDeliveryState.Acknowledged,
                AcknowledgedAtUtc = now,
                LastUpdatedAtUtc = now,
                NextRetryAtUtc = null
            })
            : messageStore.UpdateDelivery(matching.Id, record => record with
            {
                Status = AprsMessageStatus.Rejected,
                DeliveryState = AprsMessageDeliveryState.Rejected,
                FailedAtUtc = now,
                LastUpdatedAtUtc = now,
                NextRetryAtUtc = null,
                FailureReason = $"Message rejected by {packet.SourceCallsign}.",
                ValidationErrors = [.. record.ValidationErrors, $"Message rejected by {packet.SourceCallsign}."]
            });
    }

    public IReadOnlyList<AprsMessageRecord> GetMessagesDueForRetry(DateTimeOffset now)
    {
        return messageStore.GetOutboxMessages()
            .Where(message => message.DeliveryState is AprsMessageDeliveryState.WaitingForAck or AprsMessageDeliveryState.RetryPending)
            .Where(message => message.NextRetryAtUtc is not null && message.NextRetryAtUtc <= now)
            .ToArray();
    }

    public async Task<IReadOnlyList<AprsMessageRecord>> ProcessRetriesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var updated = new List<AprsMessageRecord>();
        foreach (var message in GetMessagesDueForRetry(now))
        {
            if (message.DeliveryState == AprsMessageDeliveryState.Cancelled)
            {
                continue;
            }

            if (message.RetryCount >= message.MaximumRetries)
            {
                updated.Add(messageStore.UpdateDelivery(message.Id, record => record with
                {
                    Status = AprsMessageStatus.Failed,
                    DeliveryState = AprsMessageDeliveryState.Failed,
                    FailedAtUtc = now,
                    LastUpdatedAtUtc = now,
                    NextRetryAtUtc = null,
                    FailureReason = "Maximum message retries reached.",
                    ValidationErrors = [.. record.ValidationErrors, "Maximum message retries reached."]
                }));
                continue;
            }

            var rawPacket = message.GeneratedPacket ?? FormatMessagePacket(message, message.MessageId ?? messageIdGenerator.NextId());
            var result = await transmitService.SendAsync(rawPacket, cancellationToken);
            var retryCount = message.RetryCount + 1;
            updated.Add(result.IsSuccess
                ? MarkWaitingForAck(message, message.MessageId ?? ExtractMessageId(rawPacket), rawPacket, result.TimestampUtc, retryCount)
                : messageStore.UpdateDelivery(message.Id, record => record with
                {
                    Status = AprsMessageStatus.RetryPending,
                    DeliveryState = AprsMessageDeliveryState.RetryPending,
                    RetryCount = retryCount,
                    LastUpdatedAtUtc = result.TimestampUtc,
                    NextRetryAtUtc = result.TimestampUtc.Add(configuration.RetryInterval),
                    FailureReason = result.FailureReason ?? "Retry transmit failed.",
                    ValidationErrors = [.. record.ValidationErrors, result.FailureReason ?? "Retry transmit failed."]
                }));
        }

        return updated;
    }

    public AprsMessageRecord Cancel(Guid messageRecordId, DateTimeOffset now, string? reason = null)
    {
        return messageStore.UpdateDelivery(messageRecordId, record => record with
        {
            Status = AprsMessageStatus.Cancelled,
            DeliveryState = AprsMessageDeliveryState.Cancelled,
            LastUpdatedAtUtc = now,
            NextRetryAtUtc = null,
            FailureReason = reason
        });
    }

    private AprsMessageRecord MarkWaitingForAck(
        AprsMessageRecord record,
        string messageId,
        string rawPacket,
        DateTimeOffset sentAtUtc,
        int retryCount)
    {
        return messageStore.UpdateDelivery(record.Id, current => current with
        {
            MessageId = messageId,
            GeneratedPacket = rawPacket,
            Direction = AprsMessageDirection.Outgoing,
            Status = AprsMessageStatus.WaitingForAck,
            DeliveryState = AprsMessageDeliveryState.WaitingForAck,
            RetryCount = retryCount,
            MaximumRetries = configuration.MaximumRetries,
            FirstSentAtUtc = current.FirstSentAtUtc ?? sentAtUtc,
            LastSentAtUtc = sentAtUtc,
            SentAtUtc = sentAtUtc,
            LastUpdatedAtUtc = sentAtUtc,
            NextRetryAtUtc = sentAtUtc.Add(configuration.RetryInterval),
            FailureReason = null
        });
    }

    private string FormatMessagePacket(AprsMessageRecord record, string messageId)
    {
        var source = record.LocalStationCallsign.Trim().ToUpperInvariant();
        var recipient = record.Recipient.Trim().ToUpperInvariant().PadRight(9)[..9];
        return $"{source}>{configuration.Destination}::{recipient}:{record.MessageBody}{{{messageId}";
    }

    private static string ExtractMessageId(string rawPacket)
    {
        var index = rawPacket.LastIndexOf('{');
        return index >= 0 && index < rawPacket.Length - 1 ? rawPacket[(index + 1)..] : string.Empty;
    }
}
