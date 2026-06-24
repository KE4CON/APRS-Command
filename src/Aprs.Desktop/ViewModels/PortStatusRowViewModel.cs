using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class PortStatusRowViewModel
{
    public PortStatusRowViewModel(AprsPortSnapshot port)
    {
        PortId = port.PortId;
        PortName = port.PortName;
        PortType = port.PortType.ToString();
        ConnectionState = port.ConnectionState.ToString();
        ReceiveEnabled = port.ReceiveEnabled ? "Yes" : "No";
        TransmitEnabled = port.TransmitEnabled ? "Yes" : "No";
        PacketsReceived = port.PacketCountReceived.ToString();
        PacketsTransmitted = port.PacketCountTransmitted.ToString();
        PacketCounts = $"{PacketsReceived} / {PacketsTransmitted}";
        LastPacketTime = FormatTimestamp(port.LastPacketReceivedUtc);
        LastError = string.IsNullOrWhiteSpace(port.LastError) ? "-" : port.LastError;
        SourceDescription = port.SourceDescription;
    }

    public string PortId { get; }

    public string PortName { get; }

    public string PortType { get; }

    public string ConnectionState { get; }

    public string ReceiveEnabled { get; }

    public string TransmitEnabled { get; }

    public string PacketsReceived { get; }

    public string PacketsTransmitted { get; }

    public string PacketCounts { get; }

    public string LastPacketTime { get; }

    public string LastError { get; }

    public string SourceDescription { get; }

    private static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "-" : timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }
}
