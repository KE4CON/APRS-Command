namespace Aprs.Services;

public enum AprsPacketSource
{
    Unknown,
    AprsIs,
    Rf,
    TcpKiss,
    SerialKiss,
    Direwolf,
    Agwpe,
    Replay,
    Simulation,
    External,
    LocalGenerated
}

public enum StationLifecycleState
{
    Active,
    Stale,
    Expired,
    Hidden
}

public sealed record StationSnapshot(
    string Callsign,
    int? Ssid,
    string RealCallsign,
    string? TacticalLabel,
    string DisplayName,
    StationLifecycleState LifecycleState,
    bool IsManuallyHidden,
    double? Latitude,
    double? Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    string? Comment,
    DateTimeOffset LastHeardUtc,
    DateTimeOffset LastPacketUtc,
    string? LastRawPacket,
    string? LastPacketType,
    int? CourseDegrees,
    int? SpeedKnots,
    int? AltitudeFeet,
    int PacketCount,
    int DuplicatePacketCount,
    IReadOnlyList<string> SourcePath,
    AprsPacketSource PacketSource,
    bool? HasMessagingCapability,
    StationWeatherSnapshot? Weather,
    string? Destination = null)
{
    /// <summary>
    /// True when packets appear to originate from a LoRa APRS device.
    /// Detected via tocall starting with APLT (TTGO T-Beam etc.) or
    /// path elements containing LORA.
    /// </summary>
    public bool IsLoRa =>
        (!string.IsNullOrEmpty(Destination) &&
         Destination.StartsWith("APLT", StringComparison.OrdinalIgnoreCase)) ||
        SourcePath.Any(p => p.Contains("LORA", StringComparison.OrdinalIgnoreCase));
}

public sealed record StationWeatherSnapshot(
    int? WindDirectionDegrees,
    int? WindSpeedMph,
    int? WindGustMph,
    int? TemperatureFahrenheit,
    int? RainLastHourHundredthsInch,
    int? RainLast24HoursHundredthsInch,
    int? RainSinceMidnightHundredthsInch,
    int? HumidityPercent,
    double? BarometricPressureMillibars,
    int? LuminosityWattsPerSquareMeter,
    int? SnowHundredthsInch,
    string RawWeatherBody,
    string? Comment);

public sealed record StationTrailPoint(
    string Callsign,
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    int? SpeedKnots,
    int? CourseDegrees,
    int? AltitudeFeet,
    AprsPacketSource PacketSource,
    string? RawPacket);

public sealed record TacticalLabel(
    string RealCallsign,
    string Label,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record StationAgingConfiguration(
    TimeSpan ActiveThreshold,
    TimeSpan StaleThreshold,
    TimeSpan ExpiredThreshold,
    TimeSpan HiddenThreshold,
    bool ShowExpiredStations,
    bool IncludeHiddenStationsInNormalLists)
{
    public static StationAgingConfiguration Default { get; } = new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(24),
        ShowExpiredStations: true,
        IncludeHiddenStationsInNormalLists: false);
}

public sealed record StationTrailConfiguration(
    int MaximumTrailPointsPerStation,
    double? MinimumDistanceMeters,
    TimeSpan? MaximumTrailAge,
    bool TrailsEnabled,
    bool AllowPerStationTrailToggle)
{
    public static StationTrailConfiguration Default { get; } = new(
        MaximumTrailPointsPerStation: 100,
        MinimumDistanceMeters: null,
        MaximumTrailAge: null,
        TrailsEnabled: true,
        AllowPerStationTrailToggle: true);
}
