namespace Aprs.Services;

public sealed record GpsdConfiguration(
    string Host,
    int Port,
    bool Enabled,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan ReadTimeout,
    string SourceName)
{
    public static GpsdConfiguration Default { get; } = new(
        Host: "127.0.0.1",
        Port: 2947,
        Enabled: false,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(5),
        ReadTimeout: TimeSpan.FromSeconds(30),
        SourceName: "gpsd");
}
