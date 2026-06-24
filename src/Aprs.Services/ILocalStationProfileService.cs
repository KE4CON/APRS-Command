namespace Aprs.Services;

public interface ILocalStationProfileService
{
    /// <summary>
    /// Gets the current local APRS station profile.
    /// </summary>
    LocalStationProfile GetCurrentProfile();

    /// <summary>
    /// Updates the current local APRS station profile and stamps its updated time.
    /// </summary>
    LocalStationProfile UpdateProfile(LocalStationProfile profile, DateTimeOffset updatedAtUtc);

    /// <summary>
    /// Resets the current local APRS station profile to safe defaults.
    /// </summary>
    LocalStationProfile ResetToDefaults(DateTimeOffset now);

    /// <summary>
    /// Validates the supplied local APRS station profile.
    /// </summary>
    StationProfileValidationResult ValidateProfile(LocalStationProfile profile);

    /// <summary>
    /// Validates the supplied local APRS station profile for a specific transport configuration context.
    /// </summary>
    StationProfileValidationResult ValidateProfile(LocalStationProfile profile, StationProfileValidationOptions options);

    /// <summary>
    /// Returns the formatted station identifier, including SSID when present.
    /// </summary>
    string GetFullStationIdentifier(LocalStationProfile profile);

    /// <summary>
    /// Returns whether the supplied profile is currently safe to transmit.
    /// </summary>
    bool IsSafeToTransmit(LocalStationProfile profile);

    /// <summary>
    /// Returns whether the supplied profile is safe to transmit for a specific transport configuration context.
    /// </summary>
    bool IsSafeToTransmit(LocalStationProfile profile, StationProfileValidationOptions options);
}
