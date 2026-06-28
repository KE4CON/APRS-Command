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
    DateTimeOffset? LastTransmitTimestampUtc,
    IReadOnlyList<(string Host, int Port)>? FailoverServers = null)
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
        LastTransmitTimestampUtc: null,
        FailoverServers: null);

    public AprsIsClientConfiguration WithServer(AprsIsServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return this with
        {
            ServerHost = server.HostName,
            ServerPort = server.Port
        };
    }

    /// <summary>
    /// Returns the full ordered list of servers to try — primary first, then failovers.
    /// </summary>
    public IReadOnlyList<(string Host, int Port)> AllServers()
    {
        var list = new List<(string, int)> { (ServerHost, ServerPort) };
        if (FailoverServers is not null)
            list.AddRange(FailoverServers);
        return list;
    }
}
