using Aprs.Core;

namespace Aprs.Services;

public interface IAprsMessageRetryEngine
{
    /// <summary>
    /// Queues, formats, and sends an outgoing message through the configured safe transmit service.
    /// </summary>
    Task<AprsMessageRecord> SendMessageAsync(Guid messageRecordId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Applies an incoming ACK or REJ packet to any matching outgoing message.
    /// </summary>
    AprsMessageRecord? ProcessAckOrRej(MessageAprsPacket packet, DateTimeOffset now);

    /// <summary>
    /// Returns messages whose retry timer has elapsed.
    /// </summary>
    IReadOnlyList<AprsMessageRecord> GetMessagesDueForRetry(DateTimeOffset now);

    /// <summary>
    /// Retries all due messages without requiring live time delays.
    /// </summary>
    Task<IReadOnlyList<AprsMessageRecord>> ProcessRetriesAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels an outgoing message and prevents future retries.
    /// </summary>
    AprsMessageRecord Cancel(Guid messageRecordId, DateTimeOffset now, string? reason = null);
}
