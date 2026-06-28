namespace Aprs.Desktop.Configuration;

/// <summary>PTT method for the managed local modem.</summary>
public enum PttMethod
{
    /// <summary>No PTT — VOX or radio handles keying automatically.</summary>
    None,

    /// <summary>RTS pin on a serial port.</summary>
    Rts,

    /// <summary>DTR pin on a serial port.</summary>
    Dtr,

    /// <summary>Raspberry Pi GPIO pin — for headless field server PTT without a serial adapter.</summary>
    GpioPin,
}

/// <summary>
/// Persisted configuration for the managed local modem (Direwolf launched by APRS Command).
/// </summary>
public sealed record ManagedModemSettings(
    bool Enabled,
    string AudioInputDevice,
    string AudioOutputDevice,
    PttMethod PttMethod,
    string PttSerialPort,
    int PttGpioPin,
    int CallsignSsid,
    string DirewolfPath,
    int KissPort,
    bool TransmitEnabled)
{
    public static ManagedModemSettings Default { get; } = new(
        Enabled:           false,
        AudioInputDevice:  string.Empty,
        AudioOutputDevice: string.Empty,
        PttMethod:         PttMethod.None,
        PttSerialPort:     string.Empty,
        PttGpioPin:        17,
        CallsignSsid:      0,
        DirewolfPath:      string.Empty,
        KissPort:          8001,
        TransmitEnabled:   false);
}
