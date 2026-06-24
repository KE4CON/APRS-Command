namespace Aprs.Services;

public sealed record AprsObjectEditModel(
    string ObjectName,
    double? Latitude,
    double? Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    char? Overlay,
    string Comment,
    TimeSpan? TransmitInterval,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    bool IsAlive,
    bool IsKilled,
    bool IsLocallyOwned,
    bool IsAdopted,
    string OwnerCallsign,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ValidationWarnings,
    string? PacketPreview);
