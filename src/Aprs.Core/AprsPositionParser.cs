namespace Aprs.Core;

public sealed class AprsPositionParser
{
    public PositionAprsPacket Parse(RawAprsPacket rawPacket)
    {
        var validationErrors = rawPacket.ValidationErrors.ToList();
        var information = rawPacket.Information;
        var positionType = information.Length > 0 ? information[0] : '\0';
        var hasTimestamp = positionType is '/' or '@';
        var latitudeStart = hasTimestamp ? 8 : 1;
        var timestamp = hasTimestamp && information.Length >= 8
            ? information.Substring(1, 7)
            : null;

        if (hasTimestamp && timestamp is null)
        {
            validationErrors.Add("Position packet timestamp is missing or incomplete.");
        }

        var parsedPosition = AprsPositionComponents.Parse(information, latitudeStart, "Position packet", validationErrors);
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
}
