using System.Text;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Services;

/// <summary>
/// Generates an ICS-205 Radio Communications Plan from APRS Command session data.
///
/// ICS-205 is the FEMA/NIMS standard form documenting all radio channels,
/// frequencies, and operator assignments for an incident or planned event.
/// It is typically posted at the Communications Unit (COMU) position and
/// distributed to all supervisors.
///
/// Form fields per FEMA ICS-205 (October 2017 version):
///   Block 1  — Incident name
///   Block 2  — Date/time prepared
///   Block 3  — Operational period (date/time from/to)
///   Block 4  — Basic radio channel use table
///   Block 5  — Special instructions
///   Block 6  — Prepared by (Communications Unit Leader)
/// </summary>
public static class Ics205ExportService
{
    /// <summary>
    /// Generates a plain-text ICS-205 Radio Communications Plan.
    /// </summary>
    public static string GenerateIcs205(
        string incidentName,
        string operatorCallsign,
        string operatorName,
        string icsPosition,
        DateTimeOffset periodFrom,
        DateTimeOffset periodTo,
        IReadOnlyList<FrequencyEntry> frequencies,
        IReadOnlyList<Ics205Assignment>? assignments = null,
        string? specialInstructions = null)
    {
        var sb  = new StringBuilder();
        var now = DateTimeOffset.Now;

        // ── Header ────────────────────────────────────────────────────────────
        sb.AppendLine(Repeat('═', 80));
        sb.AppendLine(Center("ICS 205 — INCIDENT RADIO COMMUNICATIONS PLAN", 80));
        sb.AppendLine(Center("FEMA / NIMS Incident Command System", 80));
        sb.AppendLine(Repeat('═', 80));
        sb.AppendLine();

        // ── Block 1: Incident name ────────────────────────────────────────────
        sb.AppendLine("1. INCIDENT NAME:");
        sb.AppendLine($"   {incidentName}");
        sb.AppendLine();

        // ── Block 2: Date/time prepared ───────────────────────────────────────
        sb.AppendLine("2. DATE/TIME PREPARED:");
        sb.AppendLine($"   {now.ToLocalTime():MM/dd/yyyy  HH:mm}");
        sb.AppendLine();

        // ── Block 3: Operational period ───────────────────────────────────────
        sb.AppendLine("3. OPERATIONAL PERIOD:");
        sb.AppendLine($"   Date/Time From:  {periodFrom.ToLocalTime():MM/dd/yyyy  HH:mm}");
        sb.AppendLine($"   Date/Time To:    {periodTo.ToLocalTime():MM/dd/yyyy  HH:mm}");
        sb.AppendLine();

        // ── Block 4: Basic radio channel use ──────────────────────────────────
        sb.AppendLine("4. BASIC RADIO CHANNEL USE:");
        sb.AppendLine(Repeat('─', 80));

        // Table header
        sb.AppendLine(
            $"   {"FUNCTION",-18} {"CH NAME / FREQ",-16} {"ASSIGNMENT",-16} {"MODE",-8} REMARKS");
        sb.AppendLine(Repeat('─', 80));

        if (frequencies.Count > 0)
        {
            foreach (var freq in frequencies)
            {
                // Find matching assignment if provided
                var assignment = assignments?.FirstOrDefault(a =>
                    string.Equals(a.FrequencyName, freq.Name, StringComparison.OrdinalIgnoreCase));

                var function   = Truncate(assignment?.Function ?? DeriveFunction(freq), 17);
                var chanFreq   = Truncate($"{freq.FrequencyMhz} MHz", 15);
                var assignee   = Truncate(assignment?.Assignee ?? string.Empty, 15);
                var mode       = Truncate(freq.Mode, 7);
                var remarks    = Truncate(freq.Notes ?? string.Empty, 20);

                sb.AppendLine($"   {function,-18} {chanFreq,-16} {assignee,-16} {mode,-8} {remarks}");
            }
        }
        else
        {
            sb.AppendLine("   No frequencies configured. Add frequencies in the Frequency Reference panel.");
        }

        sb.AppendLine(Repeat('─', 80));
        sb.AppendLine($"   Total channels: {frequencies.Count}");
        sb.AppendLine();

        // ── Block 5: Special instructions ────────────────────────────────────
        sb.AppendLine("5. SPECIAL INSTRUCTIONS:");
        if (!string.IsNullOrWhiteSpace(specialInstructions))
        {
            foreach (var line in specialInstructions.Split('\n'))
                sb.AppendLine($"   {line.Trim()}");
        }
        else
        {
            sb.AppendLine("   All operators monitor APRS (144.390 MHz) continuously.");
            sb.AppendLine("   APRS Command digital tracking active — see net control for resource status.");
            sb.AppendLine("   Contact net control before changing frequency assignments.");
        }
        sb.AppendLine();

        // ── Block 6: Prepared by ──────────────────────────────────────────────
        sb.AppendLine("6. PREPARED BY (COMU):");
        sb.AppendLine($"   Name:      {operatorName}");
        sb.AppendLine($"   Callsign:  {operatorCallsign}");
        sb.AppendLine($"   Position:  {icsPosition}");
        sb.AppendLine($"   Date/Time: {now.ToLocalTime():MM/dd/yyyy HH:mm}");
        sb.AppendLine();

        // ── Footer ────────────────────────────────────────────────────────────
        sb.AppendLine(Repeat('═', 80));
        sb.AppendLine(Center("Generated by APRS Command — github.com/KE4CON/APRS-Command", 80));
        sb.AppendLine(Center("ICS 205 (October 2017) — Page 1 of 1", 80));
        sb.AppendLine(Repeat('═', 80));

        return sb.ToString();
    }

    /// <summary>
    /// Derives a functional description from the frequency name when no explicit
    /// assignment is provided.
    /// </summary>
    private static string DeriveFunction(FrequencyEntry freq)
    {
        var name = freq.Name.ToUpperInvariant();
        if (name.Contains("APRS"))    return "APRS Digital";
        if (name.Contains("SIMPLEX")) return "Simplex";
        if (name.Contains("NOAA") || name.Contains("WX")) return "Weather (RX only)";
        if (name.Contains("ARES") || name.Contains("RACES")) return "EmComm";
        if (name.Contains("NET"))     return "Net";
        if (name.Contains("HF"))      return "HF";
        return "Operations";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string Repeat(char c, int n) => new(c, n);
    private static string Center(string s, int width) =>
        s.Length >= width ? s : s.PadLeft((width + s.Length) / 2).PadRight(width);
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>
/// An optional frequency assignment for ICS-205 Block 4 — links a frequency
/// to a specific function and operator/unit.
/// </summary>
public sealed record Ics205Assignment(
    string FrequencyName,
    string Function,
    string Assignee);
