using System.Text.RegularExpressions;

namespace Aprs.Services;

public sealed partial class LocalStationProfileService : ILocalStationProfileService
{
    private LocalStationProfile currentProfile;

    public LocalStationProfileService()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public LocalStationProfileService(DateTimeOffset now)
    {
        currentProfile = LocalStationProfile.CreateDefault(now);
    }

    public LocalStationProfile GetCurrentProfile()
    {
        return currentProfile;
    }

    public LocalStationProfile UpdateProfile(LocalStationProfile profile, DateTimeOffset updatedAtUtc)
    {
        var createdAt = profile.CreatedAtUtc == default
            ? currentProfile.CreatedAtUtc
            : profile.CreatedAtUtc;
        currentProfile = profile with
        {
            Callsign = profile.Callsign.Trim().ToUpperInvariant(),
            BeaconPath = profile.BeaconPath.Trim().ToUpperInvariant(),
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = updatedAtUtc
        };

        return currentProfile;
    }

    public LocalStationProfile ResetToDefaults(DateTimeOffset now)
    {
        currentProfile = LocalStationProfile.CreateDefault(now);
        return currentProfile;
    }

    public StationProfileValidationResult ValidateProfile(LocalStationProfile profile)
    {
        return ValidateProfile(profile, StationProfileValidationOptions.Default);
    }

    public StationProfileValidationResult ValidateProfile(LocalStationProfile profile, StationProfileValidationOptions options)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var hasCallsign = !string.IsNullOrWhiteSpace(profile.Callsign);
        if (hasCallsign && !CallsignRegex().IsMatch(profile.Callsign.Trim()))
        {
            errors.Add("Callsign must be 1-6 alphanumeric characters.");
        }

        if (profile.Ssid is < 0 or > 15)
        {
            errors.Add("SSID must be between 0 and 15.");
        }

        if (profile.FixedLatitude is < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90 degrees.");
        }

        if (profile.FixedLongitude is < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180 degrees.");
        }

        if (profile.SymbolTableIdentifier is null)
        {
            errors.Add("Symbol table identifier is required.");
        }

        if (profile.SymbolCode is null)
        {
            errors.Add("Symbol code is required.");
        }

        if (profile.FixedStationMode == profile.MobileStationMode)
        {
            errors.Add("Choose either fixed station mode or mobile station mode.");
        }

        if (profile.TransmitEnabled && !hasCallsign)
        {
            errors.Add("Transmit cannot be enabled without a valid callsign.");
        }

        if (profile.TransmitEnabled && hasCallsign && !CallsignRegex().IsMatch(profile.Callsign.Trim()))
        {
            errors.Add("Transmit cannot be enabled with an invalid callsign.");
        }

        if (profile.RfTransmitEnabled && string.IsNullOrWhiteSpace(profile.BeaconPath))
        {
            errors.Add("Beacon path is required when RF transmit is enabled.");
        }

        if (profile.AprsIsTransmitEnabled)
        {
            if (!profile.TransmitEnabled)
            {
                errors.Add("APRS-IS transmit requires the master transmit flag.");
            }

            if (!options.AprsIsTransmitConfigured)
            {
                errors.Add("APRS-IS transmit is not configured for the local station profile yet.");
            }
        }

        if (profile.RfTransmitEnabled)
        {
            if (!profile.TransmitEnabled)
            {
                errors.Add("RF transmit requires the master transmit flag.");
            }

            if (!options.RfTransmitConfigured)
            {
                errors.Add("RF transmit is not configured because no RF transport exists yet.");
            }
        }

        if (profile.TransmitEnabled && !profile.AprsIsTransmitEnabled && !profile.RfTransmitEnabled)
        {
            warnings.Add("Master transmit is enabled but no transmit transport is enabled.");
        }

        if (!profile.TransmitEnabled)
        {
            warnings.Add("Transmit is disabled.");
        }

        warnings.Add("Beaconing is not active in this phase.");

        var safeToTransmit = errors.Count == 0
            && profile.TransmitEnabled
            && ((profile.AprsIsTransmitEnabled && options.AprsIsTransmitConfigured)
                || (profile.RfTransmitEnabled && options.RfTransmitConfigured));

        return new StationProfileValidationResult(
            IsValid: errors.Count == 0,
            IsSafeToTransmit: safeToTransmit,
            Errors: errors,
            Warnings: warnings);
    }

    public string GetFullStationIdentifier(LocalStationProfile profile)
    {
        return profile.FullStationIdentifier;
    }

    public bool IsSafeToTransmit(LocalStationProfile profile)
    {
        return ValidateProfile(profile).IsSafeToTransmit;
    }

    public bool IsSafeToTransmit(LocalStationProfile profile, StationProfileValidationOptions options)
    {
        return ValidateProfile(profile, options).IsSafeToTransmit;
    }

    [GeneratedRegex("^[A-Z0-9]{1,6}$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CallsignRegex();
}
