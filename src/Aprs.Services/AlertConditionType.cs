namespace Aprs.Services;

public enum AlertConditionType
{
    Any,
    Callsign,
    NotHeardWithin,
    WindSpeedMph,
    WindGustMph,
    TemperatureFahrenheit,
    BarometricPressureMillibars,
    RainLastHourInches,
    RainLast24HoursInches,
    RainSinceMidnightInches,
    PortDisconnected,
    GpsFixInvalid,
    PacketRate,
    Bulletin,
    Message
}
