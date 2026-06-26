using Aprs.Core;

namespace Aprs.Services;

public interface IAprsMessageStoreService
{
    /// <summary>Fired when a new incoming message is added to the inbox.</summary>
    event EventHandler<AprsMessageRecord>? IncomingMessageReceived;

    /// <summary>
    /// Adds a parsed incoming APRS message packet to the inbox.
    /// </summary>
    AprsMessageRecord AddIncomingMessage(MessageAprsPacket packet, string localStationCallsign, AprsPacketSource source = AprsPacketSource.Unknown);

    /// <summary>
    /// Creates an outgoing draft message without transmitting it.
    /// </summary>
    AprsMessageRecord CreateDraft(AprsMessageComposeRequest request, DateTimeOffset now);

    /// <summary>
    /// Validates an outgoing message compose request.
    /// </summary>
    AprsMessageComposeValidationResult ValidateComposeRequest(AprsMessageComposeRequest request);

    /// <summary>
    /// Queues a draft for future transmit without sending it.
    /// </summary>
    AprsMessageRecord QueueMessage(Guid messageRecordId, DateTimeOffset now);

    /// <summary>
    /// Marks a queued or outgoing message as sent.
    /// </summary>
    AprsMessageRecord MarkSent(Guid messageRecordId, DateTimeOffset sentAtUtc);

    /// <summary>
    /// Marks a queued or outgoing message as failed.
    /// </summary>
    AprsMessageRecord MarkFailed(Guid messageRecordId, DateTimeOffset failedAtUtc, string failureReason);

    /// <summary>
    /// Updates delivery metadata for ACK/retry processing.
    /// </summary>
    AprsMessageRecord UpdateDelivery(Guid messageRecordId, Func<AprsMessageRecord, AprsMessageRecord> update);

    IReadOnlyList<AprsMessageRecord> GetAllMessages();

    IReadOnlyList<AprsMessageRecord> GetInboxMessages();

    IReadOnlyList<AprsMessageRecord> GetOutboxMessages();

    IReadOnlyList<AprsMessageRecord> GetDrafts();

    IReadOnlyList<AprsMessageRecord> GetMessagesByRemoteStation(string remoteStationCallsign);

    IReadOnlyList<AprsMessageRecord> GetConversation(string remoteStationCallsign);

    void Clear();
}
