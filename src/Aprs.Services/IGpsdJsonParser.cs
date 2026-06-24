namespace Aprs.Services;

public interface IGpsdJsonParser
{
    /// <summary>
    /// Parses one gpsd JSON report without requiring a live gpsd connection.
    /// </summary>
    GpsdParseResult Parse(string rawJson, string sourceName = "gpsd", DateTimeOffset? receivedAtUtc = null);
}
