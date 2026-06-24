namespace Aprs.Services;

public sealed record AprsPortSnapshot(
    string PortId,
    string PortName,
    AprsPortType PortType,
    bool Enabled,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    AprsPortConnectionState ConnectionState,
    DateTimeOffset? LastConnectedUtc,
    DateTimeOffset? LastDisconnectedUtc,
    DateTimeOffset? LastPacketReceivedUtc,
    DateTimeOffset? LastPacketTransmittedUtc,
    int PacketCountReceived,
    int PacketCountTransmitted,
    string? LastError,
    string SourceDescription,
    string? ConfigurationReference)
{
    public AprsPacketSource PacketSource => AprsPortSourceMapper.ToPacketSource(PortType);

    public bool IsReceiveAvailable => Enabled && ReceiveEnabled;

    public bool IsTransmitConfigured => Enabled && TransmitEnabled;
}
