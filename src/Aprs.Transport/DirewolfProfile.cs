namespace Aprs.Transport;

public sealed record DirewolfProfile(
    string ProfileName,
    string Host,
    int KissPort,
    bool Enabled,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    bool AutoReconnect,
    TimeSpan ReconnectDelay,
    string SourceName,
    string? Notes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc)
{
    public static DirewolfProfile CreateDefault(DateTimeOffset timestampUtc)
    {
        return new DirewolfProfile(
            ProfileName: "Local Direwolf",
            Host: "127.0.0.1",
            KissPort: 8001,
            Enabled: false,
            ReceiveEnabled: true,
            TransmitEnabled: false,
            AutoReconnect: true,
            ReconnectDelay: TimeSpan.FromSeconds(5),
            SourceName: "Direwolf",
            Notes: "Local TCP KISS profile for Direwolf.",
            CreatedUtc: timestampUtc,
            UpdatedUtc: timestampUtc);
    }
}
