namespace Aprs.Services;

public enum AprsPortConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted,
    Disabled
}
