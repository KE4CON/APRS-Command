using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class PortStatusViewModel
{
    public PortStatusViewModel(IAprsPortManager portManager)
    {
        var summary = portManager.GetHealthSummary();
        Ports = portManager.GetAllPorts().Select(port => new PortStatusRowViewModel(port)).ToArray();
        Summary = $"{summary.EnabledPorts}/{summary.TotalPorts} enabled, {summary.ConnectedPorts} connected, {summary.TotalPacketsReceived} RX, {summary.TotalPacketsTransmitted} TX";
        Health = summary.HasErrors ? string.Join(" ", summary.Errors) : "No port errors recorded.";
    }

    public IReadOnlyList<PortStatusRowViewModel> Ports { get; }

    public string Summary { get; }

    public string Health { get; }

    public static PortStatusViewModel CreateDesignTime()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("aprs-is", "APRS-IS", AprsPortType.AprsIs, "Internet APRS server") with
        {
            Enabled = true,
            ConnectionState = AprsPortConnectionState.Connected,
            LastConnectedUtc = DateTimeOffset.UtcNow.AddMinutes(-20)
        });
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("tcp-kiss", "TCP KISS", AprsPortType.TcpKiss, "Network KISS TNC"));
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("serial-kiss", "Serial KISS", AprsPortType.SerialKiss, "USB/serial KISS TNC"));
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("direwolf", "Direwolf", AprsPortType.Direwolf, "Local Direwolf TCP KISS"));
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("agwpe", "AGWPE", AprsPortType.Agwpe, "AGWPE-compatible packet engine"));
        manager.RecordPacketReceived("aprs-is", DateTimeOffset.UtcNow.AddSeconds(-30));

        return new PortStatusViewModel(manager);
    }
}
