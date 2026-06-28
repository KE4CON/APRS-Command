using System.Text;
using Aprs.Services;

namespace Aprs.Desktop.Services;

/// <summary>
/// Generates after-action reports from the current session's data — stations heard,
/// messages sent and received, and raw packet log. Outputs CSV (for served agencies
/// who need to open it in Excel/Numbers) and plain text summary.
/// </summary>
public static class AfterActionReportService
{
    // ── CSV exports ───────────────────────────────────────────────────

    /// <summary>Generates a CSV of all stations heard during the session.</summary>
    public static string GenerateStationsCsv(
        IReadOnlyCollection<StationSnapshot> stations,
        DateTimeOffset reportTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# APRS Command — Stations Heard Report");
        sb.AppendLine($"# Generated: {reportTime.ToLocalTime():dddd, MMMM d, yyyy HH:mm:ss zzz}");
        sb.AppendLine($"# Total stations: {stations.Count}");
        sb.AppendLine("#");
        sb.AppendLine("Callsign,Last Heard (Local),Latitude,Longitude,Speed (kts),Course (°),Comment");

        foreach (var s in stations.OrderByDescending(s => s.LastHeardUtc))
        {
            sb.AppendLine(string.Join(",",
                CsvField(s.Callsign),
                CsvField(s.LastHeardUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                s.Latitude.HasValue  ? $"{s.Latitude:F6}"  : "",
                s.Longitude.HasValue ? $"{s.Longitude:F6}" : "",
                s.SpeedKnots.HasValue ? s.SpeedKnots.ToString() : "",
                s.CourseDegrees.HasValue ? s.CourseDegrees.ToString() : "",
                CsvField(s.Comment ?? string.Empty)));
        }

        return sb.ToString();
    }

    /// <summary>Generates a CSV of all messages (sent and received) during the session.</summary>
    public static string GenerateMessagesCsv(
        IReadOnlyList<AprsMessageRecord> messages,
        DateTimeOffset reportTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# APRS Command — Messages Report");
        sb.AppendLine($"# Generated: {reportTime.ToLocalTime():dddd, MMMM d, yyyy HH:mm:ss zzz}");
        sb.AppendLine($"# Total messages: {messages.Count}");
        sb.AppendLine("#");
        sb.AppendLine("Direction,Status,Time (Local),From,To,Body");

        foreach (var m in messages.OrderBy(m => m.SentAtUtc ?? DateTimeOffset.MinValue))
        {
            var time = m.SentAtUtc.HasValue
                ? m.SentAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : string.Empty;

            sb.AppendLine(string.Join(",",
                CsvField(m.Direction.ToString()),
                CsvField(m.Status.ToString()),
                CsvField(time),
                CsvField(m.LocalStationCallsign),
                CsvField(m.RemoteStationCallsign),
                CsvField(m.MessageBody)));
        }

        return sb.ToString();
    }

    /// <summary>Generates a CSV of the raw packet log.</summary>
    public static string GeneratePacketLogCsv(
        IReadOnlyList<RawPacketLogEntry> entries,
        DateTimeOffset reportTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# APRS Command — Raw Packet Log");
        sb.AppendLine($"# Generated: {reportTime.ToLocalTime():dddd, MMMM d, yyyy HH:mm:ss zzz}");
        sb.AppendLine($"# Total packets: {entries.Count}");
        sb.AppendLine("#");
        sb.AppendLine("Time (Local),Source Callsign,Source,Raw Packet");

        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(",",
                CsvField(e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                CsvField(e.SourceCallsign ?? string.Empty),
                CsvField(e.PacketSource.ToString()),
                CsvField(e.RawPacketText)));
        }

        return sb.ToString();
    }

    // ── Text summary ──────────────────────────────────────────────────

    /// <summary>Generates a plain text after-action summary suitable for filing.</summary>
    public static string GenerateTextSummary(
        string operatorCallsign,
        string eventName,
        DateTimeOffset sessionStart,
        DateTimeOffset reportTime,
        IReadOnlyCollection<StationSnapshot> stations,
        IReadOnlyList<AprsMessageRecord> messages,
        IReadOnlyList<RawPacketLogEntry> packets)
    {
        var sb = new StringBuilder();
        var duration = reportTime - sessionStart;

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("                  APRS COMMAND AFTER-ACTION REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Event:           {eventName}");
        sb.AppendLine($"Operator:        {operatorCallsign.ToUpperInvariant()}");
        sb.AppendLine($"Report generated:{reportTime.ToLocalTime():dddd, MMMM d, yyyy} at {reportTime.ToLocalTime():HH:mm:ss zzz}");
        sb.AppendLine($"Session start:   {sessionStart.ToLocalTime():HH:mm:ss}");
        sb.AppendLine($"Session duration:{(int)duration.TotalHours:D2}h {duration.Minutes:D2}m");
        sb.AppendLine();

        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("STATIONS HEARD");
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine($"Total stations:  {stations.Count}");
        sb.AppendLine();

        foreach (var s in stations.OrderByDescending(s => s.LastHeardUtc).Take(50))
        {
            var pos = s.Latitude.HasValue && s.Longitude.HasValue
                ? $"{s.Latitude:F4}°N {Math.Abs(s.Longitude.Value):F4}°W"
                : "no position";
            var lastHeard = s.LastHeardUtc.ToLocalTime().ToString("HH:mm:ss");
            sb.AppendLine($"  {s.Callsign,-10} last heard {lastHeard}   {pos}   {s.Comment ?? string.Empty}");
        }

        if (stations.Count > 50)
            sb.AppendLine($"  ... and {stations.Count - 50} more. See stations CSV for full list.");

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("MESSAGES");
        sb.AppendLine("───────────────────────────────────────────────────────────");

        var sent     = messages.Count(m => m.Direction == AprsMessageDirection.Outgoing);
        var received = messages.Count(m => m.Direction == AprsMessageDirection.Incoming);
        var acked    = messages.Count(m => m.Status    == AprsMessageStatus.Acknowledged);

        sb.AppendLine($"Total messages:  {messages.Count}  ({sent} sent, {received} received, {acked} acknowledged)");
        sb.AppendLine();

        foreach (var m in messages.OrderBy(m => m.SentAtUtc ?? DateTimeOffset.MinValue))
        {
            var dir  = m.Direction == AprsMessageDirection.Outgoing ? "→" : "←";
            var time = m.SentAtUtc.HasValue ? m.SentAtUtc.Value.ToLocalTime().ToString("HH:mm:ss") : "??:??:??";
            var peer = m.Direction == AprsMessageDirection.Outgoing
                ? m.RemoteStationCallsign : m.LocalStationCallsign;
            sb.AppendLine($"  {time}  {dir} {peer,-10}  [{m.Status,-12}]  {m.MessageBody}");
        }

        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("PACKET STATISTICS");
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine($"Total packets:   {packets.Count}");

        var bySource = packets
            .GroupBy(p => p.PacketSource.ToString())
            .OrderByDescending(g => g.Count());
        foreach (var g in bySource)
            sb.AppendLine($"  {g.Key,-20} {g.Count(),6} packets");

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("End of Report — APRS Command v0.2.0 — github.com/KE4CON/APRS-Command");
        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    // ── Helper ────────────────────────────────────────────────────────

    private static string CsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
