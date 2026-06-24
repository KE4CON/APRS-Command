namespace Aprs.Core;

public sealed class AprsParser : IAprsParser
{
    private readonly AprsPositionParser positionParser = new();
    private readonly AprsTelemetryParser telemetryParser = new();
    private readonly AprsMessageParser messageParser = new();
    private readonly AprsObjectItemParser objectItemParser = new();
    private readonly AprsWeatherParser weatherParser = new();

    public bool TryParse(string rawLine, DateTimeOffset receivedAtUtc, out AprsPacket? packet, out string? error)
    {
        var parsed = Parse(rawLine, receivedAtUtc);
        packet = parsed;
        error = parsed.ValidationErrors.FirstOrDefault();

        return parsed.IsValid;
    }

    public AprsPacket Parse(string? rawLine, DateTimeOffset receivedAtUtc)
    {
        var originalLine = rawLine ?? string.Empty;
        var validationErrors = new List<string>();

        if (rawLine is null)
        {
            validationErrors.Add("Packet line is null.");
        }

        var workingLine = originalLine.Trim();
        if (workingLine.Length == 0)
        {
            validationErrors.Add("Packet line is empty.");
            return CreateRawPacket(
                originalLine,
                string.Empty,
                null,
                string.Empty,
                [],
                string.Empty,
                receivedAtUtc,
                validationErrors);
        }

        var separatorIndex = workingLine.IndexOf(':');
        if (separatorIndex < 0)
        {
            validationErrors.Add("Packet is missing ':' information separator.");
        }

        var header = separatorIndex >= 0 ? workingLine[..separatorIndex] : workingLine;
        var information = separatorIndex >= 0 ? workingLine[(separatorIndex + 1)..] : string.Empty;

        var sourceSeparatorIndex = header.IndexOf('>');
        if (sourceSeparatorIndex < 0)
        {
            validationErrors.Add("Packet is missing '>' source separator.");
        }

        var sourceText = sourceSeparatorIndex >= 0 ? header[..sourceSeparatorIndex] : string.Empty;
        var destinationAndPath = sourceSeparatorIndex >= 0 ? header[(sourceSeparatorIndex + 1)..] : header;
        var (sourceCallsign, sourceSsid) = ParseSource(sourceText, validationErrors);
        var (destination, path) = ParseDestinationAndPath(destinationAndPath, validationErrors);

        var rawPacket = CreateRawPacket(
            originalLine,
            sourceCallsign,
            sourceSsid,
            destination,
            path,
            information,
            receivedAtUtc,
            validationErrors);

        if (weatherParser.CanParse(rawPacket.Information))
        {
            return weatherParser.Parse(rawPacket);
        }

        if (IsPositionInformation(rawPacket.Information))
        {
            return positionParser.Parse(rawPacket);
        }

        if (rawPacket.Information.StartsWith('>'))
        {
            var statusText = rawPacket.Information[1..];
            return new StatusAprsPacket(
                rawPacket.RawLine,
                rawPacket.SourceCallsign,
                rawPacket.SourceSsid,
                rawPacket.Destination,
                rawPacket.Path,
                rawPacket.Information,
                rawPacket.ReceivedAtUtc,
                rawPacket.IsValid,
                rawPacket.ValidationErrors,
                rawPacket.QConstruct,
                statusText,
                statusText);
        }

        if (rawPacket.Information.StartsWith('<'))
        {
            return new CapabilityAprsPacket(
                rawPacket.RawLine,
                rawPacket.SourceCallsign,
                rawPacket.SourceSsid,
                rawPacket.Destination,
                rawPacket.Path,
                rawPacket.Information,
                rawPacket.ReceivedAtUtc,
                rawPacket.IsValid,
                rawPacket.ValidationErrors,
                rawPacket.QConstruct,
                rawPacket.Information[1..]);
        }

        if (telemetryParser.CanParse(rawPacket.Information))
        {
            return telemetryParser.Parse(rawPacket);
        }

        if (messageParser.CanParse(rawPacket.Information))
        {
            return messageParser.Parse(rawPacket);
        }

        if (objectItemParser.CanParse(rawPacket.Information))
        {
            return objectItemParser.Parse(rawPacket);
        }

        if (rawPacket.Information.StartsWith('?'))
        {
            return new QueryAprsPacket(
                rawPacket.RawLine,
                rawPacket.SourceCallsign,
                rawPacket.SourceSsid,
                rawPacket.Destination,
                rawPacket.Path,
                rawPacket.Information,
                rawPacket.ReceivedAtUtc,
                rawPacket.IsValid,
                rawPacket.ValidationErrors,
                rawPacket.QConstruct,
                rawPacket.Information);
        }

        if (!rawPacket.IsValid || string.IsNullOrEmpty(rawPacket.Information))
        {
            return rawPacket;
        }

        return new UnknownAprsPacket(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            rawPacket.Destination,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            rawPacket.IsValid,
            rawPacket.ValidationErrors,
            rawPacket.QConstruct);
    }

    private static RawAprsPacket CreateRawPacket(
        string rawLine,
        string sourceCallsign,
        int? sourceSsid,
        string destination,
        IReadOnlyList<string> path,
        string information,
        DateTimeOffset receivedAtUtc,
        IReadOnlyList<string> validationErrors)
    {
        var qConstruct = path.FirstOrDefault(IsQConstruct);

        return new RawAprsPacket(
            rawLine,
            sourceCallsign,
            sourceSsid,
            destination,
            path,
            information,
            receivedAtUtc,
            validationErrors.Count == 0,
            validationErrors,
            qConstruct);
    }

    private static (string Callsign, int? Ssid) ParseSource(string sourceText, List<string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            validationErrors.Add("Packet source callsign is missing.");
            return (string.Empty, null);
        }

        var sourceParts = sourceText.Split('-', 2);
        var callsign = sourceParts[0];
        if (!IsValidCallsign(callsign))
        {
            validationErrors.Add("Packet source callsign is invalid.");
        }

        if (sourceParts.Length == 1)
        {
            return (callsign, null);
        }

        if (!int.TryParse(sourceParts[1], out var ssid) || ssid is < 0 or > 15)
        {
            validationErrors.Add("Packet source SSID is invalid.");
            return (callsign, null);
        }

        return (callsign, ssid);
    }

    private static (string Destination, IReadOnlyList<string> Path) ParseDestinationAndPath(
        string destinationAndPath,
        List<string> validationErrors)
    {
        var components = destinationAndPath.Split(',', StringSplitOptions.None);
        var destination = components.Length > 0 ? components[0] : string.Empty;
        if (string.IsNullOrWhiteSpace(destination))
        {
            validationErrors.Add("Packet destination is missing.");
        }

        if (components.Any(component => component.Length == 0))
        {
            validationErrors.Add("Packet header contains an empty destination or path component.");
        }

        var path = components
            .Skip(1)
            .Where(component => component.Length > 0)
            .ToArray();

        return (destination, path);
    }

    private static bool IsValidCallsign(string callsign)
    {
        return callsign.Length is >= 1 and <= 9
            && callsign.All(char.IsLetterOrDigit);
    }

    private static bool IsQConstruct(string pathComponent)
    {
        return pathComponent.Length >= 2
            && pathComponent[0] is 'q' or 'Q';
    }

    private static bool IsPositionInformation(string information)
    {
        return information.Length > 0
            && information[0] is '!' or '=' or '/' or '@';
    }
}
