namespace Aprs.Core;

public sealed class AprsPositionParser
{
    public PositionAprsPacket Parse(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var information = rawPacket.Information;
        var positionType = information.Length > 0 ? information[0] : '\0';
        var hasTimestamp = positionType is '/' or '@';

        // Most timestamps are 7 chars: DHM (DDHHMMz / DDHHMM/) or HMS (HHMMSSh), whose 7th char is a
        // non-digit designator (z, h or /). The MDHM form (MMDDHHMM) is 8 all-digit chars with no
        // designator, so latitude then starts one byte later. Reading a fixed 7 chars misparsed every
        // MDHM position (audit Parser M2): the position field was read starting mid-timestamp.
        var isMdhm = hasTimestamp && information.Length > 7 && char.IsDigit(information[7]);
        var timestampLength = isMdhm ? 8 : 7;
        var latitudeStart = hasTimestamp ? 1 + timestampLength : 1;
        var timestamp = hasTimestamp && information.Length >= 1 + timestampLength
            ? information.Substring(1, timestampLength)
            : null;

        if (hasTimestamp && timestamp is null)
        {
            validationErrors.Add("Position packet timestamp is missing or incomplete.");
        }

        // Spec §9: detect compressed position by checking whether the character
        // at the latitude-start position is a Symbol Table Identifier (/ or \)
        // rather than a digit character. Normal lat/long always starts with digits.
        if (AprsCompressedPositionDecoder.IsCompressed(information, latitudeStart))
        {
            return ParseCompressed(rawPacket, validationErrors, positionType, timestamp, latitudeStart);
        }

        var parsedPosition = AprsPositionComponents.Parse(
            information, latitudeStart, "Position packet", validationErrors);
        var (courseDegrees, speedKnots) = AprsPositionComponents.ParseCourseAndSpeed(parsedPosition.Comment);
        var altitudeFeet = AprsPositionComponents.ParseAltitude(parsedPosition.Comment);

        return new PositionAprsPacket(
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
            positionType,
            timestamp,
            parsedPosition.Latitude,
            parsedPosition.Longitude,
            parsedPosition.SymbolTableIdentifier,
            parsedPosition.SymbolCode,
            parsedPosition.Comment,
            courseDegrees,
            speedKnots,
            altitudeFeet,
            parsedPosition.PositionAmbiguity);
    }

    private static PositionAprsPacket ParseCompressed(
        RawAprsPacket rawPacket,
        List<string> validationErrors,
        char positionType,
        string? timestamp,
        int latitudeStart)
    {
        var compressed = AprsCompressedPositionDecoder.Decode(
            rawPacket.Information, latitudeStart, "Position packet", validationErrors);

        // Apply the !DAO! precision extension if the comment carries one.
        var (daoLatitude, daoLongitude, daoComment) =
            AprsDaoExtension.Apply(compressed.Latitude, compressed.Longitude, compressed.Comment);

        return new PositionAprsPacket(
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
            positionType,
            timestamp,
            daoLatitude,
            daoLongitude,
            compressed.SymbolTableIdentifier,
            compressed.SymbolCode,
            daoComment,
            compressed.CourseDegrees,
            compressed.SpeedKnots,
            compressed.AltitudeFeet,
            0); // Compressed position has no ambiguity (spec §9 p.36)
    }
}
