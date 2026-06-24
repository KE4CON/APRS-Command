namespace Aprs.Transport;

public enum TcpKissConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted
}
