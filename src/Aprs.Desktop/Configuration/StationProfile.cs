using System.Globalization;

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

    /// <summary>
    /// Loads the saved profile, or <see cref="Default"/> if none is saved or it is invalid.
    /// Routes through the unified <see cref="JsonAppSettingsStore"/> so the station profile is one
    /// section of the single settings file rather than a separate file.
    /// </summary>
    public static StationProfile Load() => JsonAppSettingsStore.Default.Load().Station;

    /// <summary>
    /// Saves this profile as the station section of the unified settings file, leaving every other
    /// section untouched.
    /// </summary>
    public void Save() => JsonAppSettingsStore.Default.Update(settings => settings with { Station = this });
}
