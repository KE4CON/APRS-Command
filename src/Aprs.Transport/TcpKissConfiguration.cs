namespace Aprs.Transport;

public sealed record TcpKissConfiguration(
    string Host,
    int Port,
    bool Enabled,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    string SourceName,
    TimeSpan ConnectionTimeout,
    TimeSpan ReadTimeout)
{
    public static TcpKissConfiguration Default { get; } = new(
        Host: "127.0.0.1",
        Port: 8001,
        Enabled: false,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(5),
        ReceiveEnabled: true,
        TransmitEnabled: false,
        SourceName: "Direwolf TCP KISS",
        ConnectionTimeout: TimeSpan.FromSeconds(10),
        ReadTimeout: TimeSpan.FromSeconds(30));
}
