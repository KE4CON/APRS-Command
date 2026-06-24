namespace Aprs.Core;

/// <summary>
/// Parses APRS text monitor lines into packet models without throwing on malformed input.
/// </summary>
public interface IAprsParser
{
    /// <summary>
    /// Attempts to parse a raw APRS text line.
    /// </summary>
    /// <param name="rawLine">The original APRS text line.</param>
    /// <param name="receivedAtUtc">The time the line was received.</param>
    /// <param name="packet">The parsed packet, including validation errors when malformed.</param>
    /// <param name="error">The first validation error, or <see langword="null" /> when valid.</param>
    /// <returns><see langword="true" /> when the line appears valid; otherwise <see langword="false" />.</returns>
    bool TryParse(string rawLine, DateTimeOffset receivedAtUtc, out AprsPacket? packet, out string? error);
}
