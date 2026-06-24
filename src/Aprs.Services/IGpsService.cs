namespace Aprs.Services;

public interface IGpsService
{
    /// <summary>
    /// Gets the latest combined GPS position, if any NMEA position data has been accepted.
    /// </summary>
    GpsPosition? CurrentPosition { get; }

    /// <summary>
    /// Returns whether the latest combined GPS position has a valid fix and coordinates.
    /// </summary>
    bool HasValidFix { get; }

    /// <summary>
    /// Accepts one raw NMEA sentence and updates the current GPS state when parsing succeeds.
    /// </summary>
    NmeaParseResult AcceptSentence(string rawSentence, string sourceName = "NMEA", DateTimeOffset? receivedAtUtc = null);

    /// <summary>
    /// Accepts one parsed gpsd report and merges useful GPS state when parsing succeeds.
    /// </summary>
    void AcceptGpsdReport(GpsdParseResult report, string sourceName = "gpsd", DateTimeOffset? receivedAtUtc = null);

    /// <summary>
    /// Clears all GPS state.
    /// </summary>
    void Reset();
}
