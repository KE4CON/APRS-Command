namespace Aprs.Services;

public interface IGpsSentenceSource
{
    /// <summary>
    /// Future serial GPS input hook that yields raw NMEA sentences without requiring hardware in tests.
    /// </summary>
    IAsyncEnumerable<string> ReadSentencesAsync(CancellationToken cancellationToken);
}
