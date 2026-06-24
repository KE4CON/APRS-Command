namespace Aprs.Services;

public sealed record RfDiagnosticsSummary(
    int TotalPacketsAnalyzed,
    int RfPackets,
    int AprsIsPackets,
    int DuplicatePackets,
    int UniqueStations,
    IReadOnlyList<KeyValuePair<string, int>> TopPacketSources,
    IReadOnlyList<KeyValuePair<string, int>> TopTransmittingStations,
    IReadOnlyList<string> ExcessiveBeaconWarnings,
    IReadOnlyList<string> PathWarnings,
    int RfOnlyPacketCount,
    int AprsIsOnlyPacketCount,
    int SeenOnBothRfAndAprsIsPacketCount,
    DateTimeOffset? LastUpdatedTimestampUtc);
