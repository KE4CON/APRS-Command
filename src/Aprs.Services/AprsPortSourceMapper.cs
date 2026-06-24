namespace Aprs.Services;

public static class AprsPortSourceMapper
{
    public static AprsPacketSource ToPacketSource(AprsPortType portType)
    {
        return portType switch
        {
            AprsPortType.AprsIs => AprsPacketSource.AprsIs,
            AprsPortType.TcpKiss => AprsPacketSource.TcpKiss,
            AprsPortType.SerialKiss => AprsPacketSource.SerialKiss,
            AprsPortType.Direwolf => AprsPacketSource.Direwolf,
            AprsPortType.Agwpe => AprsPacketSource.Agwpe,
            AprsPortType.Replay => AprsPacketSource.Replay,
            AprsPortType.Simulation => AprsPacketSource.Simulation,
            _ => AprsPacketSource.Unknown
        };
    }

    public static bool IsRfPort(AprsPortType portType)
    {
        return portType is AprsPortType.TcpKiss or AprsPortType.SerialKiss or AprsPortType.Direwolf or AprsPortType.Agwpe;
    }
}
