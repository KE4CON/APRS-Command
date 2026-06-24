namespace Aprs.Services;

public interface INmeaParser
{
    /// <summary>
    /// Parses one raw NMEA sentence into a GPS position fragment without requiring GPS hardware.
    /// </summary>
    NmeaParseResult Parse(string rawSentence, string sourceName = "NMEA", DateTimeOffset? receivedAtUtc = null);
}
