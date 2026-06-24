using System.Text.RegularExpressions;
using Aprs.Core;

namespace Aprs.Services;

public sealed partial class AprsMessageStoreService : IAprsMessageStoreService
{
    private const int MaximumMessageBodyLength = 67;
    private readonly List<AprsMessageRecord> messages = [];

    public AprsMessageRecord AddIncomingMessage(MessageAprsPacket packet, string localStationCallsign, AprsPacketSource source = AprsPacketSource.Unknown)
    {
        var remoteStation = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        var kind = DetermineKind(packet);
        var now = packet.ReceivedAtUtc;
        var record = new AprsMessageRecord(
            Guid.NewGuid(),
            packet.MessageId,
            NormalizeCallsign(localStationCallsign),
            remoteStation,
            packet.Addressee,
            remoteStation,
            NormalizeCallsign(packet.Addressee),
            packet.MessageBody,
            packet.RawLine,
            AprsMessageDirection.Incoming,
            AprsMessageStatus.Received,
            now,
            SentAtUtc: null,
            ReceivedAtUtc: now,
            now,
            source,
            kind,
            packet.ValidationErrors);

        messages.Add(record);
        return record;
    }

    public AprsMessageRecord CreateDraft(AprsMessageComposeRequest request, DateTimeOffset now)
    {
        var validation = ValidateComposeRequest(request);
        var record = new AprsMessageRecord(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(request.MessageId) ? null : request.MessageId.Trim(),
            NormalizeCallsign(request.LocalStationCallsign),
            NormalizeCallsign(request.RecipientCallsign),
            NormalizeCallsign(request.RecipientCallsign),
            NormalizeCallsign(request.LocalStationCallsign),
            NormalizeCallsign(request.RecipientCallsign),
            request.MessageText.Trim(),
            RawPacket: null,
            AprsMessageDirection.Draft,
            AprsMessageStatus.Draft,
            now,
            SentAtUtc: null,
            ReceivedAtUtc: null,
            now,
            AprsPacketSource.Unknown,
            AprsMessageKind.PrivateMessage,
            validation.Errors);

        messages.Add(record);
        return record;
    }

    public AprsMessageComposeValidationResult ValidateComposeRequest(AprsMessageComposeRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.RecipientCallsign))
        {
            errors.Add("Recipient callsign is required.");
        }
        else if (!CallsignRegex().IsMatch(request.RecipientCallsign.Trim()))
        {
            errors.Add("Recipient callsign is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.LocalStationCallsign))
        {
            errors.Add("Local station callsign is required.");
        }
        else if (!CallsignRegex().IsMatch(request.LocalStationCallsign.Trim()))
        {
            errors.Add("Local station callsign is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            errors.Add("Message body is required.");
        }

        if (request.MessageText.Contains('\r') || request.MessageText.Contains('\n'))
        {
            errors.Add("Message body cannot contain line breaks.");
        }

        if (request.MessageText.Trim().Length > MaximumMessageBodyLength)
        {
            errors.Add($"Message body must be {MaximumMessageBodyLength} characters or fewer.");
        }

        return errors.Count == 0
            ? AprsMessageComposeValidationResult.Success
            : new AprsMessageComposeValidationResult(IsValid: false, errors);
    }

    public AprsMessageRecord QueueMessage(Guid messageRecordId, DateTimeOffset now)
    {
        return UpdateRecord(messageRecordId, record => record with
        {
            Direction = AprsMessageDirection.Outgoing,
            Status = AprsMessageStatus.Queued,
            LastUpdatedAtUtc = now
        });
    }

    public AprsMessageRecord MarkSent(Guid messageRecordId, DateTimeOffset sentAtUtc)
    {
        return UpdateRecord(messageRecordId, record => record with
        {
            Direction = AprsMessageDirection.Outgoing,
            Status = AprsMessageStatus.Sent,
            SentAtUtc = sentAtUtc,
            LastUpdatedAtUtc = sentAtUtc
        });
    }

    public AprsMessageRecord MarkFailed(Guid messageRecordId, DateTimeOffset failedAtUtc, string failureReason)
    {
        return UpdateRecord(messageRecordId, record => record with
        {
            Direction = AprsMessageDirection.Outgoing,
            Status = AprsMessageStatus.Failed,
            LastUpdatedAtUtc = failedAtUtc,
            ValidationErrors = [.. record.ValidationErrors, string.IsNullOrWhiteSpace(failureReason) ? "Message failed." : failureReason],
            DeliveryState = AprsMessageDeliveryState.Failed,
            FailedAtUtc = failedAtUtc,
            FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "Message failed." : failureReason
        });
    }

    public AprsMessageRecord UpdateDelivery(Guid messageRecordId, Func<AprsMessageRecord, AprsMessageRecord> update)
    {
        return UpdateRecord(messageRecordId, update);
    }

    public IReadOnlyList<AprsMessageRecord> GetAllMessages()
    {
        return messages.OrderBy(message => message.CreatedAtUtc).ToArray();
    }

    public IReadOnlyList<AprsMessageRecord> GetInboxMessages()
    {
        return messages
            .Where(message => message.Direction == AprsMessageDirection.Incoming)
            .OrderByDescending(message => message.ReceivedAtUtc ?? message.CreatedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<AprsMessageRecord> GetOutboxMessages()
    {
        return messages
            .Where(message => message.Direction == AprsMessageDirection.Outgoing)
            .OrderByDescending(message => message.LastUpdatedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<AprsMessageRecord> GetDrafts()
    {
        return messages
            .Where(message => message.Direction == AprsMessageDirection.Draft || message.Status == AprsMessageStatus.Draft)
            .OrderByDescending(message => message.LastUpdatedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<AprsMessageRecord> GetMessagesByRemoteStation(string remoteStationCallsign)
    {
        var normalized = NormalizeCallsign(remoteStationCallsign);
        return messages
            .Where(message => string.Equals(message.RemoteStationCallsign, normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(message => message.CreatedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<AprsMessageRecord> GetConversation(string remoteStationCallsign)
    {
        return GetMessagesByRemoteStation(remoteStationCallsign);
    }

    public void Clear()
    {
        messages.Clear();
    }

    private AprsMessageRecord UpdateRecord(Guid messageRecordId, Func<AprsMessageRecord, AprsMessageRecord> update)
    {
        var index = messages.FindIndex(message => message.Id == messageRecordId);
        if (index < 0)
        {
            throw new InvalidOperationException("Message record was not found.");
        }

        var updated = update(messages[index]);
        messages[index] = updated;
        return updated;
    }

    private static AprsMessageKind DetermineKind(MessageAprsPacket packet)
    {
        if (packet.IsQuery)
        {
            return AprsMessageKind.Query;
        }

        if (packet.IsAnnouncement)
        {
            return AprsMessageKind.Announcement;
        }

        return packet.IsBulletin ? AprsMessageKind.Bulletin : AprsMessageKind.PrivateMessage;
    }

    private static string FormatSourceCallsign(string callsign, int? ssid)
    {
        var normalized = NormalizeCallsign(callsign);
        return ssid is null ? normalized : $"{normalized}-{ssid}";
    }

    private static string NormalizeCallsign(string callsign)
    {
        return string.IsNullOrWhiteSpace(callsign)
            ? string.Empty
            : callsign.Trim().ToUpperInvariant();
    }

    [GeneratedRegex("^[A-Z0-9]{1,6}(-([0-9]|1[0-5]))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CallsignRegex();
}
