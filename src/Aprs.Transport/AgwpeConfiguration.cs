namespace Aprs.Transport;

public sealed record AgwpeConfiguration(
    string Host,
    int Port,
    bool Enabled,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    string SourceName,
    TimeSpan ConnectionTimeout,
    TimeSpan ReadTimeout,
    int SelectedRadioPort,
    string? Username,
    string? Password)
{
    public static AgwpeConfiguration Default { get; } = new(
        Host: "127.0.0.1",
        Port: 8000,
        Enabled: false,
        ReceiveEnabled: true,
        TransmitEnabled: false,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(5),
        SourceName: "AGWPE",
        ConnectionTimeout: TimeSpan.FromSeconds(10),
        ReadTimeout: TimeSpan.FromSeconds(30),
        SelectedRadioPort: 0,
        Username: null,
        Password: null);
}
