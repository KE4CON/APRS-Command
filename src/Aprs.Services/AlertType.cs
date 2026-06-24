namespace Aprs.Services;

public enum AlertType
{
    CallsignHeard,
    CallsignNotHeard,
    StationEnteredArea,
    StationLeftArea,
    WeatherThreshold,
    AprsIsDisconnected,
    TncDisconnected,
    GpsFixLost,
    ExcessiveBeaconing,
    PacketRateHigh,
    ObjectUpdated,
    MessageReceived,
    BulletinReceived,
    PortError,
    SystemError
}
