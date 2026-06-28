namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted settings for GPSD (GPS daemon) connection. GPSD is the standard
/// GPS management daemon on Linux and macOS. It listens on TCP port 2947 and
/// provides position data to multiple clients simultaneously.
///
/// <para>On Raspberry Pi: sudo apt install gpsd gpsd-clients, then connect
/// your GPS receiver and gpsd manages it automatically.</para>
/// </summary>
public sealed record GpsdSettings(
    bool Enabled,
    string Host,
    int Port)
{
    public static GpsdSettings Default { get; } = new(
        Enabled: false,
        Host:    "127.0.0.1",
        Port:    2947);
}
