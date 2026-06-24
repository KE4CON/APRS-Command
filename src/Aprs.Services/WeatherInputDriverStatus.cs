namespace Aprs.Services;

public enum WeatherInputDriverStatus
{
    Disabled,
    Stopped,
    Starting,
    Running,
    Connected,
    Disconnected,
    Reconnecting,
    Faulted,
    Stale
}
