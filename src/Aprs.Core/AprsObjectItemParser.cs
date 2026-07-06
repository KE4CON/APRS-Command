namespace Aprs.Core;

public sealed class AprsObjectItemParser
{
    private const int ObjectNameLength = 9;
    private const int ObjectTimestampLength = 7;

    public bool CanParse(string information)
    {
        return information.StartsWith(';') || information.StartsWith(')');
    }

    public AprsPacket Parse(RawAprsPacket rawPacket)
    {
        return rawPacket.Information.StartsWith(';')
            ? ParseObject(rawPacket)
            : ParseItem(rawPacket);
    }

    private static ObjectAprsPacket ParseObject(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var information = rawPacket.Information;
        var rawObjectBody = information.Length > 1 ? information[1..] : string.Empty;
        var objectName = rawObjectBody.Length >= ObjectNameLength
            ? rawObjectBody[..ObjectNameLength].TrimEnd()
            : rawObjectBody.TrimEnd();
        var liveKilledIndicatorIndex = 1 + ObjectNameLength;
        var liveKilledIndicator = TryGetChar(information, liveKilledIndicatorIndex);
        var isAlive = liveKilledIndicator == '*';
        var isKilled = liveKilledIndicator == '_'; // Real-world convention (WB4APR PROTOCOL.TXT)
        var timestampStart = liveKilledIndicatorIndex + 1;
        var timestamp = information.Length >= timestampStart + ObjectTimestampLength
            ? information.Substring(timestampStart, ObjectTimestampLength)
            : null;
        var latitudeStart = timestampStart + ObjectTimestampLength;

        if (objectName.Length == 0)
        {
            validationErrors.Add("Object name is missing.");
        }

        if (rawObjectBody.Length < ObjectNameLength)
        {
            validationErrors.Add("Object name is missing or incomplete.");
        }

        if (liveKilledIndicator is null)
        {
            validationErrors.Add("Object live/killed indicator is missing.");
        }
        else if (liveKilledIndicator is not ('*' or '_'))
        {
            validationErrors.Add("Object live/killed indicator is invalid.");
        }

        if (timestamp is null)
        {
            validationErrors.Add("Object timestamp is missing or incomplete.");
        }

        var parsedPosition = AprsPositionComponents.Parse(information, latitudeStart, "Object position", validationErrors);

        return new ObjectAprsPacket(
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
            objectName,
            isAlive,
            isKilled,
            timestamp,
            parsedPosition.Latitude,
            parsedPosition.Longitude,
            parsedPosition.SymbolTableIdentifier,
            parsedPosition.SymbolCode,
            parsedPosition.Comment,
            rawObjectBody,
            parsedPosition.PositionAmbiguity);
    }

    private static ItemAprsPacket ParseItem(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var information = rawPacket.Information;
        var rawItemBody = information.Length > 1 ? information[1..] : string.Empty;
        var separatorIndex = FindItemPositionSeparator(rawItemBody);
        var itemName = separatorIndex >= 0 ? rawItemBody[..separatorIndex].TrimEnd() : rawItemBody.TrimEnd();
        var latitudeStart = separatorIndex >= 0 ? separatorIndex + 2 : information.Length;

        if (itemName.Length == 0)
        {
            validationErrors.Add("Item name is missing.");
        }

        if (separatorIndex < 0)
        {
            validationErrors.Add("Item position separator is missing.");
        }

        var parsedPosition = AprsPositionComponents.Parse(information, latitudeStart, "Item position", validationErrors);

        return new ItemAprsPacket(
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
            itemName,
            parsedPosition.Latitude,
            parsedPosition.Longitude,
            parsedPosition.SymbolTableIdentifier,
            parsedPosition.SymbolCode,
            parsedPosition.Comment,
            rawItemBody,
            parsedPosition.PositionAmbiguity);
    }

    private static int FindItemPositionSeparator(string rawItemBody)
    {
        var liveIndex = rawItemBody.IndexOf('!', StringComparison.Ordinal);
        var killedIndex = rawItemBody.IndexOf('_', StringComparison.Ordinal);

        return liveIndex switch
        {
            >= 0 when killedIndex >= 0 => Math.Min(liveIndex, killedIndex),
            >= 0 => liveIndex,
            _ => killedIndex
        };
    }

    private static char? TryGetChar(string value, int index)
    {
        return index >= 0 && index < value.Length ? value[index] : null;
    }
}
