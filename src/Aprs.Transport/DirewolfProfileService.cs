namespace Aprs.Transport;

public sealed class DirewolfProfileService : IDirewolfProfileService
{
    private DirewolfProfile currentProfile;

    public DirewolfProfileService()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public DirewolfProfileService(DateTimeOffset timestampUtc)
    {
        currentProfile = DirewolfProfile.CreateDefault(timestampUtc);
    }

    public DirewolfProfile CreateDefaultProfile(DateTimeOffset timestampUtc)
    {
        return DirewolfProfile.CreateDefault(timestampUtc);
    }

    public DirewolfProfile GetCurrentProfile()
    {
        return currentProfile;
    }

    public DirewolfProfile UpdateProfile(DirewolfProfile profile, DateTimeOffset timestampUtc)
    {
        currentProfile = profile with { UpdatedUtc = timestampUtc };
        return currentProfile;
    }

    public DirewolfProfile ResetToDefaults(DateTimeOffset timestampUtc)
    {
        currentProfile = DirewolfProfile.CreateDefault(timestampUtc);
        return currentProfile;
    }

    public DirewolfProfileValidationResult ValidateProfile(DirewolfProfile profile, bool rfTransmitSafetyEnabled = false)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.ProfileName))
        {
            warnings.Add("Profile name is empty.");
        }

        if (string.IsNullOrWhiteSpace(profile.Host))
        {
            errors.Add("Direwolf host is required.");
        }

        if (profile.KissPort is < 1 or > 65535)
        {
            errors.Add("Direwolf KISS port must be between 1 and 65535.");
        }

        if (profile.ReconnectDelay < TimeSpan.Zero)
        {
            errors.Add("Reconnect delay cannot be negative.");
        }

        if (!profile.Enabled)
        {
            warnings.Add("Direwolf profile is disabled.");
        }

        if (!profile.ReceiveEnabled)
        {
            warnings.Add("Direwolf receive is disabled.");
        }

        if (!profile.TransmitEnabled)
        {
            warnings.Add("Direwolf transmit is disabled.");
        }
        else if (!rfTransmitSafetyEnabled)
        {
            errors.Add("Direwolf transmit requires RF transmit safety to be explicitly enabled.");
        }

        var isValid = errors.Count == 0;
        var isSafeForReceive = isValid && profile.Enabled && profile.ReceiveEnabled;
        var isSafeForTransmit = isValid && profile.Enabled && profile.TransmitEnabled && rfTransmitSafetyEnabled;

        return new DirewolfProfileValidationResult(isValid, isSafeForReceive, isSafeForTransmit, errors, warnings);
    }

    public TcpKissConfiguration ToTcpKissConfiguration(DirewolfProfile profile)
    {
        return TcpKissConfiguration.Default with
        {
            Host = profile.Host.Trim(),
            Port = profile.KissPort,
            Enabled = profile.Enabled,
            ReconnectEnabled = profile.AutoReconnect,
            ReconnectDelay = profile.ReconnectDelay,
            ReceiveEnabled = profile.ReceiveEnabled,
            TransmitEnabled = profile.TransmitEnabled,
            SourceName = string.IsNullOrWhiteSpace(profile.SourceName) ? "Direwolf" : profile.SourceName.Trim()
        };
    }

    public bool IsSafeForReceive(DirewolfProfile profile)
    {
        return ValidateProfile(profile).IsSafeForReceive;
    }

    public bool IsSafeForTransmit(DirewolfProfile profile, bool rfTransmitSafetyEnabled)
    {
        return ValidateProfile(profile, rfTransmitSafetyEnabled).IsSafeForTransmit;
    }
}
