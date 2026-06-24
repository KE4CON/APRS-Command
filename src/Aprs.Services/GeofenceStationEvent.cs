namespace Aprs.Services;

public sealed record GeofenceStationEvent(
    Guid EventId,
    Guid GeofenceId,
    string GeofenceName,
    string StationCallsign,
    GeofenceEventType EventType,
    DateTimeOffset TimestampUtc,
    double Latitude,
    double Longitude,
    AlertSeverity AlertSeverity,
    string Summary,
    string? Details);
