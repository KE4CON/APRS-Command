namespace Aprs.Transport;

public sealed record AprsIsServerDefinition(
    string Name,
    string HostName,
    int Port,
    string Description,
    bool IsEnabled,
    bool IsDefault,
    string? Region,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsCustom);
