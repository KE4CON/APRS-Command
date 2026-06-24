namespace Aprs.Transport;

public interface IDirewolfProfileService
{
    DirewolfProfile CreateDefaultProfile(DateTimeOffset timestampUtc);

    DirewolfProfile GetCurrentProfile();

    DirewolfProfile UpdateProfile(DirewolfProfile profile, DateTimeOffset timestampUtc);

    DirewolfProfile ResetToDefaults(DateTimeOffset timestampUtc);

    DirewolfProfileValidationResult ValidateProfile(DirewolfProfile profile, bool rfTransmitSafetyEnabled = false);

    TcpKissConfiguration ToTcpKissConfiguration(DirewolfProfile profile);

    bool IsSafeForReceive(DirewolfProfile profile);

    bool IsSafeForTransmit(DirewolfProfile profile, bool rfTransmitSafetyEnabled);
}
