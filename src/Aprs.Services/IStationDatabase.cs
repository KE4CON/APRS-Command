using Aprs.Core;

namespace Aprs.Services;

/// <summary>
/// Maintains latest-known APRS station state from parsed packets.
/// </summary>
public interface IStationDatabase
{
    /// <summary>
    /// Applies a parsed APRS packet to the station database.
    /// </summary>
    void ProcessPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown);

    /// <summary>
    /// Returns all known stations.
    /// </summary>
    IReadOnlyCollection<StationSnapshot> GetAllStations();

    /// <summary>
    /// Returns stations that should appear in normal station lists.
    /// </summary>
    IReadOnlyCollection<StationSnapshot> GetVisibleStations();

    /// <summary>
    /// Returns stations currently classified as active.
    /// </summary>
    IReadOnlyCollection<StationSnapshot> GetActiveStations();

    /// <summary>
    /// Returns the chronological position trail for one station.
    /// </summary>
    IReadOnlyList<StationTrailPoint> GetTrail(string callsign);

    /// <summary>
    /// Sets or updates a tactical label for one callsign.
    /// </summary>
    TacticalLabel SetTacticalLabel(string callsign, string label, string? notes, DateTimeOffset now);

    /// <summary>
    /// Removes one tactical label.
    /// </summary>
    bool RemoveTacticalLabel(string callsign);

    /// <summary>
    /// Returns one tactical label by callsign.
    /// </summary>
    TacticalLabel? GetTacticalLabel(string callsign);

    /// <summary>
    /// Returns all tactical labels.
    /// </summary>
    IReadOnlyCollection<TacticalLabel> GetAllTacticalLabels();

    /// <summary>
    /// Removes all tactical labels.
    /// </summary>
    void ClearTacticalLabels();

    /// <summary>
    /// Returns one station by callsign or callsign-SSID.
    /// </summary>
    StationSnapshot? GetStation(string callsign);

    /// <summary>
    /// Recalculates station lifecycle states for the supplied time.
    /// </summary>
    void UpdateAgeStates(DateTimeOffset now);

    /// <summary>
    /// Manually hides one station.
    /// </summary>
    bool HideStation(string callsign);

    /// <summary>
    /// Clears manual hidden state for one station and recalculates its age state.
    /// </summary>
    bool UnhideStation(string callsign, DateTimeOffset now);

    /// <summary>
    /// Clears all manual hidden states and recalculates age states.
    /// </summary>
    void ClearHiddenState(DateTimeOffset now);

    /// <summary>
    /// Removes trail history for one station.
    /// </summary>
    bool ClearTrail(string callsign);

    /// <summary>
    /// Removes all trail history.
    /// </summary>
    void ClearAllTrails();

    /// <summary>
    /// Removes all station state.
    /// </summary>
    void Clear();
}
