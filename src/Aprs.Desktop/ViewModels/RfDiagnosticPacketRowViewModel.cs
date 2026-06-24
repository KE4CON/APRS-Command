using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class RfDiagnosticPacketRowViewModel
{
    public RfDiagnosticPacketRowViewModel(RfDiagnosticPacket packet)
    {
        Time = packet.ReceivedTimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Source = packet.ReceivedPortOrSource;
        Callsign = string.IsNullOrWhiteSpace(packet.SourceCallsign) ? "-" : packet.SourceCallsign;
        PacketType = string.IsNullOrWhiteSpace(packet.ParsedPacketType) ? "Unknown" : packet.ParsedPacketType;
        PacketSource = packet.PacketSource.ToString();
        LinkState = packet.LinkState.ToString();
        DuplicateState = packet.DuplicateState.ToString();
        Path = packet.Path.Count == 0 ? "-" : string.Join(",", packet.Path);
        HeardVia = packet.HeardViaPathComponents.Count == 0 ? "-" : string.Join(",", packet.HeardViaPathComponents);
        Warnings = packet.ValidationWarnings.Count == 0 ? "-" : string.Join(" | ", packet.ValidationWarnings);
        RawPacket = packet.RawPacket;
    }

    public string Time { get; }

    public string Source { get; }

    public string Callsign { get; }

    public string PacketType { get; }

    public string PacketSource { get; }

    public string LinkState { get; }

    public string DuplicateState { get; }

    public string Path { get; }

    public string HeardVia { get; }

    public string Warnings { get; }

    public string RawPacket { get; }
}
