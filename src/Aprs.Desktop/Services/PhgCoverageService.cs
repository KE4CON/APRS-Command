namespace Aprs.Desktop.Services;

/// <summary>
/// Calculates APRS PHG (Power-Height-Gain-Directivity) coverage footprints.
///
/// PHG is a standard APRS extension that encodes a station's transmitter power,
/// antenna height, antenna gain, and beam heading. From these values the effective
/// radio range (line-of-sight distance) can be estimated using the standard APRS
/// range formula, giving an approximate coverage circle on the map.
///
/// PHG string format: PHGphgd
///   p = Power code (0-9) → 0,1,4,9,16,25,36,49,64,81 watts
///   h = Height code (0-9) → 10,20,40,80,160,320,640,1280,2560,5120 feet HAAT
///   g = Gain code   (0-9) → 0-9 dBd antenna gain
///   d = Directivity (0-8) → omni(0), NE, E, SE, S, SW, W, NW, N
/// </summary>
public static class PhgCoverageService
{
    private static readonly int[] PowerWatts =
        [0, 1, 4, 9, 16, 25, 36, 49, 64, 81];

    private static readonly int[] HeightFeet =
        [10, 20, 40, 80, 160, 320, 640, 1280, 2560, 5120];

    private static readonly string[] DirectivityLabels =
        ["Omni", "NE", "E", "SE", "S", "SW", "W", "NW", "N"];

    /// <summary>
    /// Parses a PHG string and returns the coverage parameters.
    /// Returns null if the string is not a valid PHG value.
    /// </summary>
    public static PhgParameters? ParsePhg(string? phgString)
    {
        if (string.IsNullOrWhiteSpace(phgString)) return null;

        // Strip "PHG" prefix if present
        var s = phgString.Trim().ToUpperInvariant();
        if (s.StartsWith("PHG")) s = s[3..];
        if (s.Length < 4) return null;

        if (!int.TryParse(s[0].ToString(), out var p) || p < 0 || p > 9) return null;
        if (!int.TryParse(s[1].ToString(), out var h) || h < 0 || h > 9) return null;
        if (!int.TryParse(s[2].ToString(), out var g) || g < 0 || g > 9) return null;
        if (!int.TryParse(s[3].ToString(), out var d) || d < 0 || d > 8) return null;

        var powerW  = PowerWatts[p];
        var heightFt = HeightFeet[h];
        var gainDbd  = (double)g;
        var dirLabel = DirectivityLabels[d];

        // ERP = Power × linear gain factor (dBd → linear: 10^(dBd/10))
        var linearGain = Math.Pow(10.0, gainDbd / 10.0);
        var erp        = powerW * linearGain;

        // APRS range formula: Range (miles) = sqrt(2 * HAAT_ft * sqrt(ERP / 10))
        // Convert to km: 1 mile = 1.60934 km
        var rangeMiles = Math.Sqrt(2.0 * heightFt * Math.Sqrt(erp / 10.0));
        var rangeKm    = rangeMiles * 1.60934;

        return new PhgParameters(
            PowerWatts:   powerW,
            HeightFeet:   heightFt,
            GainDbd:      gainDbd,
            DirectivityLabel: dirLabel,
            DirectivityCode:  d,
            RangeKm:      Math.Max(rangeKm, 1.0));  // minimum 1 km
    }

    /// <summary>
    /// Calculates the PHG string and estimated range from actual station specs.
    /// Finds the closest PHG code for each parameter.
    /// </summary>
    public static PhgCalculation Calculate(
        double powerWatts,
        double haatFeet,
        double gainDbd,
        int directivityCode)
    {
        // Find closest power code
        int pCode = 0;
        double bestPDiff = double.MaxValue;
        for (int i = 0; i < PowerWatts.Length; i++)
        {
            var diff = Math.Abs(PowerWatts[i] - powerWatts);
            if (diff < bestPDiff) { bestPDiff = diff; pCode = i; }
        }

        // Find closest height code
        int hCode = 0;
        double bestHDiff = double.MaxValue;
        for (int i = 0; i < HeightFeet.Length; i++)
        {
            var diff = Math.Abs(HeightFeet[i] - haatFeet);
            if (diff < bestHDiff) { bestHDiff = diff; hCode = i; }
        }

        // Gain is 0-9 dBd directly (clamp)
        int gCode = (int)Math.Round(Math.Clamp(gainDbd, 0, 9));

        // Directivity 0-8
        int dCode = Math.Clamp(directivityCode, 0, 8);

        var phgString = $"PHG{pCode}{hCode}{gCode}{dCode}";
        var phgParams = ParsePhg(phgString)!;

        return new PhgCalculation(
            PhgString:       phgString,
            ActualPowerW:    powerWatts,
            EncodedPowerW:   PowerWatts[pCode],
            ActualHaatFt:    haatFeet,
            EncodedHaatFt:   HeightFeet[hCode],
            ActualGainDbd:   gainDbd,
            EncodedGainDbd:  gCode,
            DirectivityCode: dCode,
            DirectivityLabel: DirectivityLabels[dCode],
            RangeKm:         phgParams.RangeKm,
            Summary:         FormatSummary(phgParams));
    }

    /// <summary>Returns a human-readable summary of the PHG parameters.</summary>
    public static string FormatSummary(PhgParameters p) =>
        $"{p.PowerWatts}W · {p.HeightFeet}ft HAAT · {p.GainDbd:F0}dBd · {p.DirectivityLabel} · ~{p.RangeKm:F1} km range";
}

public sealed record PhgCalculation(
    string PhgString,
    double ActualPowerW,
    int    EncodedPowerW,
    double ActualHaatFt,
    int    EncodedHaatFt,
    double ActualGainDbd,
    int    EncodedGainDbd,
    int    DirectivityCode,
    string DirectivityLabel,
    double RangeKm,
    string Summary);

public sealed record PhgParameters(
    int PowerWatts,
    int HeightFeet,
    double GainDbd,
    string DirectivityLabel,
    int DirectivityCode,
    double RangeKm);
