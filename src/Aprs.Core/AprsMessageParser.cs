namespace Aprs.Core;

public sealed class AprsMessageParser
{
    private const int AddresseeLength = 9;

    public bool CanParse(string information)
    {
        return information.StartsWith(':');
    }

    public MessageAprsPacket Parse(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var addressee = string.Empty;
        var rawMessageBody = string.Empty;

        if (rawPacket.Information.Length < AddresseeLength + 2)
        {
            validationErrors.Add("Message packet is missing addressee or body separator.");
        }
        else
        {
            addressee = rawPacket.Information.Substring(1, AddresseeLength).TrimEnd();

            if (rawPacket.Information[AddresseeLength + 1] != ':')
            {
                validationErrors.Add("Message packet is missing second ':' body separator.");
            }
            else
            {
                rawMessageBody = rawPacket.Information[(AddresseeLength + 2)..];
            }
        }

        if (string.IsNullOrWhiteSpace(addressee))
        {
            validationErrors.Add("Message addressee is missing.");
        }

        var messageBody = rawMessageBody;
        string? messageId = null;
        string? replyAckAcknowledgedId = null;
        var messageIdIndex = rawMessageBody.LastIndexOf('{');
        if (messageIdIndex >= 0)
        {
            var idPortion = rawMessageBody[(messageIdIndex + 1)..];
            messageBody = rawMessageBody[..messageIdIndex];

            // Reply-ack (aprs.org/aprs11/replyacks.txt): "{MM}AA" carries this message's id (MM) followed
            // by an acknowledgement (AA) of the message being replied to. Previously the whole "MM}AA" was
            // stored as the message id (audit Parser M3). Split on '}'.
            var replyAckIndex = idPortion.IndexOf('}');
            if (replyAckIndex >= 0)
            {
                var acked = idPortion[(replyAckIndex + 1)..];
                replyAckAcknowledgedId = acked.Length > 0 ? acked : null;
                idPortion = idPortion[..replyAckIndex];
            }

            messageId = idPortion;
            if (messageId.Length == 0)
            {
                validationErrors.Add("Message ID is empty.");
            }
        }

        var acknowledgedMessageId = ReadAckRejId(rawMessageBody, "ack", "ACK", validationErrors);
        var rejectedMessageId = ReadAckRejId(rawMessageBody, "rej", "REJ", validationErrors);

        // A reply-ack (…{MM}AA) also carries an acknowledgement of the replied-to message.
        acknowledgedMessageId ??= replyAckAcknowledgedId;

        // Bulletin addressing (spec §14): "BLN" + one identifier char, then either 5 spaces or, for
        // group bulletins, a group name. A DIGIT identifier is a general/group bulletin; a LETTER
        // identifier is an announcement. Only the char right after "BLN" decides — a group name that
        // happens to contain letters (e.g. "BLN1WX") must NOT be read as an announcement.
        var isBulletin = addressee.StartsWith("BLN", StringComparison.OrdinalIgnoreCase);
        string? bulletinId = null;
        string? bulletinGroup = null;
        var isAnnouncement = false;
        if (isBulletin && addressee.Length > 3)
        {
            var identifier = addressee[3];
            bulletinId = identifier.ToString();
            isAnnouncement = char.IsLetter(identifier);
            if (!isAnnouncement && addressee.Length > 4)
            {
                var group = addressee[4..].TrimEnd();
                bulletinGroup = group.Length > 0 ? group : null;
            }
        }

        var isQuery = rawMessageBody.StartsWith('?');

        return new MessageAprsPacket(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            rawPacket.Destination,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            rawPacket.IsValid && validationErrors.Count == 0,
            validationErrors,
            rawPacket.QConstruct,
            addressee,
            rawMessageBody,
            messageBody,
            messageId,
            acknowledgedMessageId,
            rejectedMessageId,
            isBulletin,
            bulletinId,
            bulletinGroup,
            isAnnouncement,
            isQuery,
            isQuery ? rawMessageBody : null);
    }

    // Reads an "ackMMMMM" / "rejMMMMM" acknowledgement id from the message body, or null if the body is not
    // an acknowledgement. The APRS spec defines "ack"/"rej" as lowercase literals whose entire remaining
    // body is a message number, so:
    //   - "ACK…"/"REJ…" (uppercase) is ordinary prose, not an ack (Ordinal comparison).
    //   - lowercase prose such as "acknowledge please" is NOT an ack either, because the remainder is not a
    //     plausible message id (audit Parser M3) — only a truly empty remainder is flagged as a missing id.
    private static string? ReadAckRejId(string body, string command, string label, List<string> validationErrors)
    {
        if (!body.StartsWith(command, StringComparison.Ordinal))
        {
            return null;
        }

        var id = body[command.Length..];
        // A reply-ack style acknowledgement can carry a trailing '}' ("ack003}"); drop it.
        if (id.EndsWith('}'))
        {
            id = id[..^1];
        }

        if (id.Length == 0)
        {
            validationErrors.Add($"{label} message ID is missing.");
            return null;
        }

        return IsPlausibleMessageId(id) ? id : null;
    }

    // An APRS message number is 1–5 alphanumeric characters (classic numeric or the newer alphanumeric
    // reply-ack form). Anything longer or containing spaces/punctuation is message prose, not an id.
    private static bool IsPlausibleMessageId(string id)
    {
        if (id.Length is < 1 or > 5)
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
