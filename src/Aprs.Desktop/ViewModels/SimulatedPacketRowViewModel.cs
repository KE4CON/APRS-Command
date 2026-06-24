using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class SimulatedPacketRowViewModel
{
    public SimulatedPacketRowViewModel(SimulatedAprsPacket packet)
    {
        Time = packet.GeneratedAtUtc.ToLocalTime().ToString("HH:mm:ss");
        Kind = packet.PacketKind;
        EntityName = string.IsNullOrWhiteSpace(packet.EntityName) ? "-" : packet.EntityName;
        Source = packet.PacketSource.ToString();
        RawPacket = packet.RawPacket;
    }

    public string Time { get; }

    public string Kind { get; }

    public string EntityName { get; }

    public string Source { get; }

    public string RawPacket { get; }
}
