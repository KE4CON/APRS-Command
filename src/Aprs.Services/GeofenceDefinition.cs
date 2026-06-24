namespace Aprs.Services;

public sealed record GeofenceDefinition(
    Guid GeofenceId,
    string Name,
    string? Description,
    bool Enabled,
    GeofenceType GeofenceType,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    IReadOnlyList<GeofencePoint> PolygonPoints,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Notes,
    string? DisplayStyle,
    bool AlertOnEnter,
    bool AlertOnExit,
    AlertSeverity AlertSeverity,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings);
