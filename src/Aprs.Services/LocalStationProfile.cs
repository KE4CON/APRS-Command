namespace Aprs.Services;

public sealed record LocalStationProfile(
    string Callsign,
    int? Ssid,
    double? FixedLatitude,
    double? FixedLongitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    char? Overlay,
    string StationComment,
    string? PhgData,
    string BeaconPath,
    TimeSpan AprsIsBeaconInterval,
    TimeSpan RfBeaconInterval,
    bool FixedStationMode,
    bool MobileStationMode,
    bool TransmitEnabled,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public string FullStationIdentifier
    {
        get
        {
            var callsign = string.IsNullOrWhiteSpace(Callsign) ? "N0CALL" : Callsign.Trim().ToUpperInvariant();
            return Ssid is null ? callsign : $"{callsign}-{Ssid}";
        }
    }

    public static LocalStationProfile CreateDefault(DateTimeOffset now)
    {
        return new LocalStationProfile(
            Callsign: string.Empty,
            Ssid: null,
            FixedLatitude: null,
            FixedLongitude: null,
            SymbolTableIdentifier: '/',
            SymbolCode: '-',
            Overlay: null,
            StationComment: "APRS Command",
            PhgData: null,
            BeaconPath: "WIDE1-1,WIDE2-1",
            AprsIsBeaconInterval: TimeSpan.FromMinutes(30),
            RfBeaconInterval: TimeSpan.FromMinutes(60),
            FixedStationMode: true,
            MobileStationMode: false,
            TransmitEnabled: false,
            AprsIsTransmitEnabled: false,
            RfTransmitEnabled: false,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
