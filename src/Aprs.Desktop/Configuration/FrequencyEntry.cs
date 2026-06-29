namespace Aprs.Desktop.Configuration;

/// <summary>
/// A single entry in the operator's frequency reference panel.
/// </summary>
public sealed record FrequencyEntry(
    string Name,
    string FrequencyMhz,
    string Mode,
    string? Notes)
{
    /// <summary>Default APRS and EmComm frequencies pre-loaded into every new installation.</summary>
    public static IReadOnlyList<FrequencyEntry> Defaults { get; } =
    [
        // ── APRS ──────────────────────────────────────────────────────────
        new("APRS (North America)",     "144.390", "FM / 1200 baud",  "Primary APRS frequency in the US and Canada"),
        new("APRS (Europe)",            "144.800", "FM / 1200 baud",  "Primary APRS frequency in Europe"),
        new("APRS (Australia/NZ)",      "145.175", "FM / 1200 baud",  "Primary APRS frequency in Australia and New Zealand"),
        new("APRS (Japan)",             "144.640", "FM / 1200 baud",  "Primary APRS frequency in Japan"),
        new("APRS Alternate (NA)",      "144.340", "FM / 1200 baud",  "Alternate APRS frequency, some US regions"),
        new("APRS HF (30m)",            "10.151",  "USB / 300 baud",  "HF APRS — 30 metre band"),
        new("APRS HF (20m)",            "14.105",  "USB / 300 baud",  "HF APRS — 20 metre band"),

        // ── EmComm calling frequencies ────────────────────────────────────
        new("ARES/RACES National (2m)", "146.520", "FM Simplex",      "National VHF simplex calling frequency"),
        new("ARES/RACES National (70cm)","446.000","FM Simplex",      "National UHF simplex calling frequency"),
        new("SHARES HF (Primary)",      "5.1965",  "USB",             "SHARES primary HF net frequency"),
        new("NTS National (40m)",       "7.228",   "USB",             "National Traffic System — 40 metre net"),
        new("NTS National (80m)",       "3.995",   "USB",             "National Traffic System — 80 metre net"),
        new("WX-1 (NOAA Weather)",      "162.400", "NFM Receive",     "NOAA Weather Radio — most areas"),
        new("WX-2 (NOAA Weather)",      "162.425", "NFM Receive",     "NOAA Weather Radio — alternate"),
        new("WX-3 (NOAA Weather)",      "162.450", "NFM Receive",     "NOAA Weather Radio — alternate"),
        new("WX-4 (NOAA Weather)",      "162.475", "NFM Receive",     "NOAA Weather Radio — alternate"),
        new("WX-5 (NOAA Weather)",      "162.500", "NFM Receive",     "NOAA Weather Radio — alternate"),
        new("WX-6 (NOAA Weather)",      "162.525", "NFM Receive",     "NOAA Weather Radio — alternate"),
        new("WX-7 (NOAA Weather)",      "162.550", "NFM Receive",     "NOAA Weather Radio — alternate"),
    ];
}
