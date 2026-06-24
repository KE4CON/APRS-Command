namespace Aprs.Services;

public enum GpsdConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted
}
