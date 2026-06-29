using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Transmits APRS position packets on behalf of non-APRS stations.
/// A "shadow beacon" allows an operator to place a station on the APRS map
/// without that station having their own APRS radio — useful for tracking
/// vehicles, personnel, or assets during events and activations.
///
/// The packet is transmitted using the operator's callsign with a comment
/// identifying it as a shadow beacon, following standard APRS practice.
/// </summary>
public sealed class ShadowBeaconService
{
    private readonly IAprsIsClient aprsIsClient;
    private readonly string operatorCallsign;

    // Active shadow stations: callsign → last known position
    private readonly Dictionary<string, ShadowStation> stations = new(StringComparer.OrdinalIgnoreCase);

    public ShadowBeaconService(IAprsIsClient aprsIsClient, string operatorCallsign)
    {
        this.aprsIsClient    = aprsIsClient;
        this.operatorCallsign = operatorCallsign;
    }

    public IReadOnlyDictionary<string, ShadowStation> ActiveStations => stations;

    /// <summary>
    /// Transmits a position packet for a non-APRS station and stores it
    /// for periodic re-beaconing.
    /// </summary>
    public async Task<ShadowBeaconResult> TransmitAsync(
        string trackedCallsign,
        double latitude,
        double longitude,
        char symbolTable,
        char symbolCode,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackedCallsign))
            return ShadowBeaconResult.Fail("Callsign is required.");

        var now    = DateTimeOffset.UtcNow;
        var packet = BuildPacket(trackedCallsign, latitude, longitude,
                                 symbolTable, symbolCode, comment, now);

        try
        {
            var result = await aprsIsClient.SendRawPacketAsync(
                packet, transmitConfirmed: true, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
                return ShadowBeaconResult.Fail(result.FailureReason ?? "Transmission failed.");

            stations[trackedCallsign] = new ShadowStation(
                trackedCallsign, latitude, longitude,
                symbolTable, symbolCode, comment, now);

            return ShadowBeaconResult.Ok(packet);
        }
        catch (Exception ex)
        {
            return ShadowBeaconResult.Fail(ex.Message);
        }
    }

    /// <summary>Removes a shadow station — stops re-beaconing it.</summary>
    public void Remove(string callsign) => stations.Remove(callsign);

    /// <summary>Clears all shadow stations.</summary>
    public void ClearAll() => stations.Clear();

    /// <summary>
    /// Builds a standard APRS position packet for a shadow beacon.
    /// Format: OPERATOR>APRS:)CALLSIGN!DDMM.MMN/YYYYY.YYW$comment
    /// Uses APRS item format so the tracked callsign appears as a named object.
    /// </summary>
    private static string BuildPacket(
        string callsign, double lat, double lon,
        char symTable, char symCode, string? comment, DateTimeOffset now)
    {
        // Encode latitude: DDMM.MM[NS]
        var latAbs  = Math.Abs(lat);
        var latDeg  = (int)latAbs;
        var latMin  = (latAbs - latDeg) * 60.0;
        var latHemi = lat >= 0 ? 'N' : 'S';
        var latStr  = $"{latDeg:D2}{latMin:00.00}{latHemi}";

        // Encode longitude: DDDMM.MM[EW]
        var lonAbs  = Math.Abs(lon);
        var lonDeg  = (int)lonAbs;
        var lonMin  = (lonAbs - lonDeg) * 60.0;
        var lonHemi = lon >= 0 ? 'E' : 'W';
        var lonStr  = $"{lonDeg:D3}{lonMin:00.00}{lonHemi}";

        // Pad callsign to 9 chars for APRS item format
        var paddedCall = callsign.PadRight(9)[..9];
        var commentStr = string.IsNullOrWhiteSpace(comment) ? "[Shadow beacon]" : comment.Trim();

        // APRS item packet: )NAME!position/symbol+comment
        return $"){{paddedCall}}!{latStr}{symTable}{lonStr}{symCode}{commentStr}"
               .Replace("{paddedCall}", paddedCall);
    }
}

public sealed record ShadowStation(
    string Callsign,
    double Latitude,
    double Longitude,
    char SymbolTable,
    char SymbolCode,
    string? Comment,
    DateTimeOffset LastTransmittedUtc);

public sealed record ShadowBeaconResult(bool IsSuccess, string? Packet, string? Error)
{
    public static ShadowBeaconResult Ok(string packet) => new(true, packet, null);
    public static ShadowBeaconResult Fail(string error) => new(false, null, error);
}
