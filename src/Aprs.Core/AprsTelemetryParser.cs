using System.Globalization;

namespace Aprs.Core;

public sealed class AprsTelemetryParser
{
    private static readonly string[] MetadataPrefixes = ["PARM.", "UNIT.", "EQNS.", "BITS."];

    public bool CanParse(string information)
    {
        return information.StartsWith("T#", StringComparison.Ordinal)
            || MetadataPrefixes.Any(prefix => information.StartsWith(prefix, StringComparison.Ordinal))
            || TryGetMessageEmbeddedMetadata(information, out _);
    }

    public AprsPacket Parse(RawAprsPacket rawPacket)
    {
        var information = rawPacket.Information;

        if (information.StartsWith("T#", StringComparison.Ordinal))
        {
            return ParseTelemetryValues(rawPacket);
        }

        // Telemetry metadata is defined by the spec as a message addressed to the station
        // (":ADDRESSEE :PARM.…"); accept that form as well as the bare info-field form.
        if (TryGetMessageEmbeddedMetadata(information, out var embedded))
        {
            return ParseMetadata(rawPacket, embedded);
        }

        return ParseMetadata(rawPacket, information);
    }

    // Detects the message-embedded metadata form ":<9-char addressee>:PARM./UNIT./EQNS./BITS.…" and
    // returns the metadata text (from the prefix onward), so the same parser handles both wire forms.
    private static bool TryGetMessageEmbeddedMetadata(string information, out string metadataText)
    {
        metadataText = string.Empty;
        const int addresseeLength = 9;
        if (information.Length < addresseeLength + 2
            || information[0] != ':'
            || information[addresseeLength + 1] != ':')
        {
            return false;
        }

        var body = information[(addresseeLength + 2)..];
        if (MetadataPrefixes.Any(prefix => body.StartsWith(prefix, StringComparison.Ordinal)))
        {
            metadataText = body;
            return true;
        }

        return false;
    }

    private static TelemetryAprsPacket ParseTelemetryValues(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var body = rawPacket.Information[2..];
        var components = body.Split(',', StringSplitOptions.None);
        int? sequenceNumber = null;
        var analogValues = new List<int>();
        var digitalValues = Array.Empty<bool>();

        if (components.Length == 0 || !int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSequence))
        {
            validationErrors.Add("Telemetry sequence number is invalid.");
        }
        else
        {
            sequenceNumber = parsedSequence;
        }

        foreach (var valueText in components.Skip(1).Take(5))
        {
            if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var analogValue))
            {
                validationErrors.Add("Telemetry analog value is invalid.");
                continue;
            }

            analogValues.Add(analogValue);
        }

        if (components.Length > 6)
        {
            digitalValues = ParseDigitalValues(components[6], validationErrors);
        }

        return new TelemetryAprsPacket(
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
            body,
            sequenceNumber,
            analogValues,
            digitalValues);
    }

    private static TelemetryMetadataAprsPacket ParseMetadata(RawAprsPacket rawPacket, string metadataText)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var separatorIndex = metadataText.IndexOf('.', StringComparison.Ordinal);
        var kind = separatorIndex > 0 ? metadataText[..separatorIndex] : metadataText;
        var body = separatorIndex >= 0 ? metadataText[(separatorIndex + 1)..] : string.Empty;
        var values = body.Split(',', StringSplitOptions.None);
        IReadOnlyList<bool> bitValues = [];
        string? projectTitle = null;

        if (kind == "BITS")
        {
            if (values.Length > 0)
            {
                bitValues = ParseDigitalValues(values[0], validationErrors);
            }

            projectTitle = values.Length > 1
                ? string.Join(',', values.Skip(1))
                : null;
        }

        return new TelemetryMetadataAprsPacket(
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
            kind,
            body,
            values,
            bitValues,
            projectTitle);
    }

    private static bool[] ParseDigitalValues(string valueText, List<string> validationErrors)
    {
        if (valueText.Length == 0 || valueText.Any(character => character is not ('0' or '1')))
        {
            validationErrors.Add("Telemetry digital value is invalid.");
            return [];
        }

        return valueText.Select(character => character == '1').ToArray();
    }
}
