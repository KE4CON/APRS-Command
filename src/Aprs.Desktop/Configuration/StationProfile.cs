using System.Globalization;
using System.Text.Json;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Per-user station identity (callsign + home position) used to center the map, build the
/// APRS-IS area filter, and log in to APRS-IS. Stored as JSON in the per-user application
/// data folder so every operator who runs the app has their own profile. Nothing here is
/// hardcoded to a single station; an unconfigured install falls back to <see cref="Default"/>.
/// </summary>
public sealed record StationProfile(
    string Callsign,
    double Latitude,
    double Longitude,
    int FilterRadiusKm)
{
    // Neutral fallback used before the user has saved a profile (continental US center).
    public static StationProfile Default { get; } = new("N0CALL", 39.5, -98.35, 200);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Callsign)
        && !string.Equals(Callsign, "N0CALL", StringComparison.OrdinalIgnoreCase);

    // Server-side APRS-IS range filter in the form "r/<lat>/<lon>/<km>". Invariant culture so
    // the decimal point is always "." regardless of the operator's regional settings.
    public string BuildAprsIsFilter() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "r/{0}/{1}/{2}",
            Latitude,
            Longitude,
            FilterRadiusKm);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AprsCommand",
        "station-profile.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Loads the saved profile, or returns <see cref="Default"/> if none exists or it is invalid.</summary>
    public static StationProfile Load()
    {
        try
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                var profile = JsonSerializer.Deserialize<StationProfile>(File.ReadAllText(path));
                if (profile is not null && !string.IsNullOrWhiteSpace(profile.Callsign))
                {
                    return profile;
                }
            }
        }
        catch
        {
            // Any read/parse error falls back to the safe default rather than crashing startup.
        }

        return Default;
    }

    /// <summary>Saves this profile as JSON in the per-user application data folder.</summary>
    public void Save()
    {
        var path = FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
