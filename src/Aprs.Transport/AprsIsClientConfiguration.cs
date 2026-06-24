namespace Aprs.Transport;

public sealed record AprsIsClientConfiguration(
    string ServerHost,
    int ServerPort,
    string Callsign,
    string Passcode,
    string ApplicationName,
    string ApplicationVersion,
    string? Filter,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    bool ReceiveOnly,
    bool TransmitEnabled,
    bool RequireTransmitConfirmation,
    string? DefaultTransmitSource,
    DateTimeOffset? LastTransmitTimestampUtc)
{
    public static AprsIsClientConfiguration Default { get; } = new(
        ServerHost: "rotate.aprs2.net",
        ServerPort: 14580,
        Callsign: string.Empty,
        Passcode: "-1",
        ApplicationName: "APRSCommand",
        ApplicationVersion: "0.1.0",
        Filter: null,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(10),
        ReceiveOnly: true,
        TransmitEnabled: false,
        RequireTransmitConfirmation: true,
        DefaultTransmitSource: null,
        LastTransmitTimestampUtc: null);

    public AprsIsClientConfiguration WithServer(AprsIsServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return this with
        {
            ServerHost = server.HostName,
            ServerPort = server.Port
        };
    }
}
