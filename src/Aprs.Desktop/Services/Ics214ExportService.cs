using System.Text;
using Aprs.Services;

namespace Aprs.Desktop.Services;

/// <summary>
/// Generates an ICS-214 Activity Log from APRS Command session data.
///
/// ICS-214 is the FEMA/NIMS standard Activity Log form used by served agencies
/// to document communications activities during an incident or exercise.
/// The generated file can be printed, attached to an incident package, or
/// emailed to an emergency manager.
///
/// Form fields per FEMA ICS-214 (October 2017 version):
///   Block 1  — Incident name
///   Block 2  — Operational period (date/time from/to)
///   Block 3  — Name, ICS position, home agency (the operator)
///   Block 4  — Resources assigned (stations heard / net control roster)
///   Block 5  — Activity log (timestamped events: check-ins, messages, alerts)
///   Block 6  — Prepared by
/// </summary>
public static class Ics214ExportService
{
    /// <summary>
    /// Generates a plain-text ICS-214 Activity Log suitable for printing or filing.
    /// </summary>
    public static string GenerateIcs214(
        string incidentName,
        string operatorName,
        string operatorCallsign,
        string icsPosition,
        string homeAgency,
        DateTimeOffset periodFrom,
        DateTimeOffset periodTo,
        IReadOnlyCollection<StationSnapshot> stations,
        IReadOnlyCollection<AprsMessageSnapshot> messages,
        IReadOnlyCollection<NetControlRosterEntry>? rosterEntries = null)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;

        // ── Header ────────────────────────────────────────────────────────────
        sb.AppendLine(Repeat('═', 72));
        sb.AppendLine(Center("ICS 214 — ACTIVITY LOG", 72));
        sb.AppendLine(Center("FEMA / NIMS Incident Command System", 72));
        sb.AppendLine(Repeat('═', 72));
        sb.AppendLine();

        // ── Block 1: Incident name ────────────────────────────────────────────
        sb.AppendLine("1. INCIDENT NAME:");
        sb.AppendLine($"   {incidentName}");
        sb.AppendLine();

        // ── Block 2: Operational period ───────────────────────────────────────
        sb.AppendLine("2. OPERATIONAL PERIOD:");
        sb.AppendLine($"   Date/Time From:  {periodFrom.ToLocalTime():MM/dd/yyyy  HH:mm}");
        sb.AppendLine($"   Date/Time To:    {periodTo.ToLocalTime():MM/dd/yyyy  HH:mm}");
        sb.AppendLine();

        // ── Block 3: Operator information ─────────────────────────────────────
        sb.AppendLine("3. NAME / ICS POSITION / HOME AGENCY:");
        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine($"   {"NAME",-28} {"ICS POSITION",-20} HOME AGENCY");
        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine($"   {$"{operatorName} ({operatorCallsign})",-28} {icsPosition,-20} {homeAgency}");
        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine();

        // ── Block 4: Resources assigned ───────────────────────────────────────
        sb.AppendLine("4. RESOURCES ASSIGNED:");
        sb.AppendLine(Repeat('─', 72));

        if (rosterEntries is { Count: > 0 })
        {
            sb.AppendLine($"   {"CALLSIGN",-14} {"STATUS",-12} {"CHECK-IN",-18} NOTES");
            sb.AppendLine(Repeat('─', 72));
            foreach (var entry in rosterEntries.OrderBy(e => e.CheckInTime))
            {
                var checkIn = entry.CheckInTime.HasValue
                    ? entry.CheckInTime.Value.ToLocalTime().ToString("MM/dd HH:mm")
                    : "—";
                sb.AppendLine($"   {entry.Callsign,-14} {entry.Status,-12} {checkIn,-18} {entry.Notes ?? string.Empty}");
            }
        }
        else if (stations.Count > 0)
        {
            sb.AppendLine($"   {"CALLSIGN",-14} {"LAST HEARD",-20} {"LAT",-12} {"LON",-12} COMMENT");
            sb.AppendLine(Repeat('─', 72));
            foreach (var s in stations.OrderBy(s => s.Callsign))
            {
                var lastHeard = s.LastHeardUtc.ToLocalTime().ToString("MM/dd HH:mm");
                var lat = s.Latitude.HasValue  ? $"{s.Latitude:F4}"  : "—";
                var lon = s.Longitude.HasValue ? $"{s.Longitude:F4}" : "—";
                var comment = Truncate(s.Comment ?? string.Empty, 18);
                sb.AppendLine($"   {s.Callsign,-14} {lastHeard,-20} {lat,-12} {lon,-12} {comment}");
            }
        }
        else
        {
            sb.AppendLine("   No stations recorded.");
        }

        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine($"   Total stations: {(rosterEntries?.Count ?? stations.Count)}");
        sb.AppendLine();

        // ── Block 5: Activity log ─────────────────────────────────────────────
        sb.AppendLine("5. ACTIVITY LOG:");
        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine($"   {"DATE/TIME",-18} NOTABLE ACTIVITIES");
        sb.AppendLine(Repeat('─', 72));

        // Operational period start
        sb.AppendLine($"   {periodFrom.ToLocalTime():MM/dd/yyyy HH:mm}  Net/activation opened. Operator: {operatorCallsign}.");

        // Messages as activity entries
        var activityMessages = messages
            .Where(m => m.TimestampUtc >= periodFrom && m.TimestampUtc <= periodTo)
            .OrderBy(m => m.TimestampUtc)
            .ToList();

        foreach (var msg in activityMessages)
        {
            var time = msg.TimestampUtc.ToLocalTime().ToString("MM/dd/yyyy HH:mm");
            var direction = msg.IsOutbound ? "TX→" : "←RX";
            var text = Truncate($"{direction} {msg.FromCallsign}→{msg.ToCallsign}: {msg.MessageText}", 54);
            sb.AppendLine($"   {time,-18} {text}");
        }

        // Roster check-in events
        if (rosterEntries is { Count: > 0 })
        {
            foreach (var entry in rosterEntries
                .Where(e => e.CheckInTime.HasValue)
                .OrderBy(e => e.CheckInTime))
            {
                var time = entry.CheckInTime!.Value.ToLocalTime().ToString("MM/dd/yyyy HH:mm");
                sb.AppendLine($"   {time,-18} CHECK-IN: {entry.Callsign} ({entry.Status})");
            }
        }

        // Operational period end
        sb.AppendLine($"   {periodTo.ToLocalTime():MM/dd/yyyy HH:mm}  Net/activation closed.");
        sb.AppendLine(Repeat('─', 72));
        sb.AppendLine();

        // ── Block 6: Prepared by ──────────────────────────────────────────────
        sb.AppendLine("6. PREPARED BY:");
        sb.AppendLine($"   Name:      {operatorName}");
        sb.AppendLine($"   Callsign:  {operatorCallsign}");
        sb.AppendLine($"   Position:  {icsPosition}");
        sb.AppendLine($"   Date/Time: {now.ToLocalTime():MM/dd/yyyy HH:mm}");
        sb.AppendLine();

        // ── Footer ────────────────────────────────────────────────────────────
        sb.AppendLine(Repeat('═', 72));
        sb.AppendLine(Center("Generated by APRS Command — github.com/KE4CON/APRS-Command", 72));
        sb.AppendLine(Center("ICS 214 (October 2017) — Page 1 of 1", 72));
        sb.AppendLine(Repeat('═', 72));

        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Repeat(char c, int n) => new(c, n);
    private static string Center(string s, int width) =>
        s.Length >= width ? s : s.PadLeft((width + s.Length) / 2).PadRight(width);
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>A snapshot of a message for ICS-214 activity log purposes.</summary>
public sealed record AprsMessageSnapshot(
    DateTimeOffset TimestampUtc,
    string FromCallsign,
    string ToCallsign,
    string MessageText,
    bool IsOutbound);

/// <summary>A net control roster entry for ICS-214 Block 4.</summary>
public sealed record NetControlRosterEntry(
    string Callsign,
    string Status,
    DateTimeOffset? CheckInTime,
    string? Notes);
