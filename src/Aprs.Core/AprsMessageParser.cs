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
        var messageIdIndex = rawMessageBody.LastIndexOf('{');
        if (messageIdIndex >= 0)
        {
            messageId = rawMessageBody[(messageIdIndex + 1)..];
            messageBody = rawMessageBody[..messageIdIndex];

            if (messageId.Length == 0)
            {
                validationErrors.Add("Message ID is empty.");
            }
        }

        var acknowledgedMessageId = StartsWithCommand(rawMessageBody, "ack")
            ? rawMessageBody[3..]
            : null;
        var rejectedMessageId = StartsWithCommand(rawMessageBody, "rej")
            ? rawMessageBody[3..]
            : null;

        if (acknowledgedMessageId == string.Empty)
        {
            validationErrors.Add("ACK message ID is missing.");
            acknowledgedMessageId = null;
        }

        if (rejectedMessageId == string.Empty)
        {
            validationErrors.Add("REJ message ID is missing.");
            rejectedMessageId = null;
        }

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

    private static bool StartsWithCommand(string value, string command)
    {
        // The APRS spec defines "ack" and "rej" as lowercase literals. Matching case-insensitively
        // would misclassify an ordinary message such as "ACKNOWLEDGED" as an acknowledgement.
        return value.StartsWith(command, StringComparison.Ordinal);
    }
}
