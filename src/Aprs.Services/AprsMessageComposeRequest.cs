namespace Aprs.Services;

public sealed record AprsMessageComposeRequest(
    string LocalStationCallsign,
    string RecipientCallsign,
    string MessageText,
    string? MessageId = null);
