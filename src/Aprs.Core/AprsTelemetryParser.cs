using System.Globalization;

namespace Aprs.Core;

public sealed class AprsTelemetryParser
{
    private static readonly string[] MetadataPrefixes = ["PARM.", "UNIT.", "EQNS.", "BITS."];

    public bool CanParse(string information)
    {
        return information.StartsWith("T#", StringComparison.Ordinal)
            || MetadataPrefixes.Any(prefix => information.StartsWith(prefix, StringComparison.Ordinal));
    }

    public AprsPacket Parse(RawAprsPacket rawPacket)
    {
        if (rawPacket.Information.StartsWith("T#", StringComparison.Ordinal))
        {
            return ParseTelemetryValues(rawPacket);
        }

        return ParseMetadata(rawPacket);
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

    private static TelemetryMetadataAprsPacket ParseMetadata(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var separatorIndex = rawPacket.Information.IndexOf('.', StringComparison.Ordinal);
        var kind = separatorIndex > 0 ? rawPacket.Information[..separatorIndex] : rawPacket.Information;
        var body = separatorIndex >= 0 ? rawPacket.Information[(separatorIndex + 1)..] : string.Empty;
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
