namespace Aprs.Desktop.Configuration;

/// <summary>Persisted GPS settings for serial NMEA input.</summary>
public sealed record GpsSettings(
    bool Enabled,
    string SerialPortName,
    int BaudRate,
    bool UpdateStationPosition)
{
    public static GpsSettings Default { get; } = new(
        Enabled:                false,
        SerialPortName:         string.Empty,
        BaudRate:               4800,
        UpdateStationPosition:  false);
}
