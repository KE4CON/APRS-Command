using System.Globalization;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Complete operator station profile persisted between runs. Covers identity, position, symbol,
/// beaconing parameters, and per-destination transmit opt-ins. Nothing here is hardcoded to a
/// single station; an unconfigured install falls back to <see cref="Default"/>.
/// </summary>
public sealed record StationProfile(
    string Callsign,
    int Ssid,
    double Latitude,
    double Longitude,
    int FilterRadiusKm,
    char SymbolTable,
    char SymbolCode,
    string StationComment,
    string BeaconPath,
    int AprsIsBeaconMinutes,
    int RfBeaconMinutes,
    bool FixedStationMode,
    bool TransmitEnabled,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    string? PhgData)
{
    public static StationProfile Default { get; } = new(
        Callsign:             "N0CALL",
        Ssid:                 0,
        Latitude:             39.5,
        Longitude:            -98.35,
        FilterRadiusKm:       200,
        SymbolTable:          '/',
        SymbolCode:           '-',
        StationComment:       "APRS Command",
        BeaconPath:           "WIDE1-1,WIDE2-1",
        AprsIsBeaconMinutes:  30,
        RfBeaconMinutes:      60,
        FixedStationMode:     true,
        TransmitEnabled:      false,
        AprsIsTransmitEnabled:false,
        RfTransmitEnabled:    false,
        PhgData:              null);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Callsign)
        && !string.Equals(Callsign, "N0CALL", StringComparison.OrdinalIgnoreCase);

    /// <summary>Full callsign including SSID when non-zero, e.g. "KE4CON-7".</summary>
    public string FullCallsign => Ssid == 0 ? Callsign : $"{Callsign}-{Ssid}";

    /// <summary>Two-character APRS symbol string displayed in the UI, e.g. "/-" or "/>".</summary>
    public string SymbolDisplay => $"{SymbolTable}{SymbolCode}";

    public string BuildAprsIsFilter() =>
        string.Format(CultureInfo.InvariantCulture, "r/{0}/{1}/{2}", Latitude, Longitude, FilterRadiusKm);

    public static StationProfile Load() => JsonAppSettingsStore.Default.Load().Station;

    public void Save() => JsonAppSettingsStore.Default.Update(s => s with { Station = this });
}
