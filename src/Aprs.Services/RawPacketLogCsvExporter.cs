using System.Globalization;
using System.Text;

namespace Aprs.Services;

/// <summary>
/// Serializes captured <see cref="RawPacketLogEntry"/> rows to the CSV format the Replay
/// loader (<c>ReplayService.LoadFromFileAsync</c>) parses. The first line is a header whose
/// column names match the parser's lookups, so an exported log round-trips back into Replay.
/// Kept in the Services layer (not the UI code-behind) so it can be unit-tested.
/// </summary>
public static class RawPacketLogCsvExporter
{
    // The parser routes to CSV mode when the first non-blank line contains "RawPacketText",
    // and reads TimestampUtc / PacketSource / Direction / RawPacketText / Notes by name.
    // The extra human-readable columns are ignored on load.
    public const string HeaderLine =
        "TimestampUtc,PacketSource,Direction,SourceCallsign,PacketType,RawPacketText,Notes";

    /// <summary>Builds the full set of CSV lines (header first, one row per entry).</summary>
    public static IReadOnlyList<string> ToCsvLines(IReadOnlyList<RawPacketLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var lines = new List<string>(entries.Count + 1) { HeaderLine };
        foreach (var entry in entries)
        {
            lines.Add(ToCsvRow(entry));
        }

        return lines;
    }

    private static string ToCsvRow(RawPacketLogEntry entry)
    {
        var fields = new[]
        {
            entry.TimestampUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            entry.PacketSource.ToString(),
            entry.Direction.ToString(),
            entry.SourceCallsign ?? string.Empty,
            entry.ParsedPacketType ?? string.Empty,
            entry.RawPacketText,
            entry.Notes ?? string.Empty
        };

        return string.Join(',', fields.Select(EscapeCsvField));
    }

    private static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        if (!needsQuoting)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            if (character == '"')
            {
                builder.Append('"');
            }

            builder.Append(character);
        }

        builder.Append('"');
        return builder.ToString();
    }
}
