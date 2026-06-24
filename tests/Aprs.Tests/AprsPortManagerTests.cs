using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsPortManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AprsPortType.AprsIs, AprsPacketSource.AprsIs)]
    [InlineData(AprsPortType.TcpKiss, AprsPacketSource.TcpKiss)]
    [InlineData(AprsPortType.SerialKiss, AprsPacketSource.SerialKiss)]
    [InlineData(AprsPortType.Direwolf, AprsPacketSource.Direwolf)]
    [InlineData(AprsPortType.Agwpe, AprsPacketSource.Agwpe)]
    public void RegisterPort_AddsExpectedPortTypeAndPacketSource(AprsPortType portType, AprsPacketSource packetSource)
    {
        var manager = new AprsPortManager();

        var port = manager.RegisterPort(CreatePort(portType.ToString(), portType));

        Assert.Equal(portType, port.PortType);
        Assert.Equal(packetSource, port.PacketSource);
        Assert.Equal(port, manager.GetPort(port.PortId));
    }

    [Fact]
    public void PortsCanBeEnabledAndDisabled()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("tcp", AprsPortType.TcpKiss));

        var enabled = manager.SetPortEnabled("tcp", true, Now);
        var disabled = manager.SetPortEnabled("tcp", false, Now.AddMinutes(1));

        Assert.True(enabled);
        Assert.True(disabled);
        var port = manager.GetPort("tcp");
        Assert.NotNull(port);
        Assert.False(port.Enabled);
        Assert.Equal(AprsPortConnectionState.Disabled, port.ConnectionState);
        Assert.Equal(Now.AddMinutes(1), port.LastDisconnectedUtc);
    }

    [Fact]
    public void ReceiveEnabledPortList_Works()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("aprs-is", AprsPortType.AprsIs) with { Enabled = true, ReceiveEnabled = true });
        manager.RegisterPort(CreatePort("serial", AprsPortType.SerialKiss) with { Enabled = false, ReceiveEnabled = true });

        var receivePorts = manager.GetReceiveEnabledPorts();

        var port = Assert.Single(receivePorts);
        Assert.Equal("APRS-IS", port.PortName);
    }

    [Fact]
    public void TransmitEnabledPortList_ExcludesTransmitDisabledPorts()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("direwolf", AprsPortType.Direwolf) with { Enabled = true, TransmitEnabled = false });
        manager.RegisterPort(CreatePort("agwpe", AprsPortType.Agwpe) with { Enabled = true, TransmitEnabled = true });

        var transmitPorts = manager.GetTransmitEnabledPorts();

        var port = Assert.Single(transmitPorts);
        Assert.Equal(AprsPortType.Agwpe, port.PortType);
    }

    [Fact]
    public void PacketCountersAndLastReceivedTimestampUpdate()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("agwpe", AprsPortType.Agwpe));

        manager.RecordPacketReceived("agwpe", Now);
        manager.RecordPacketReceived("agwpe", Now.AddSeconds(1));
        manager.RecordPacketTransmitted("agwpe", Now.AddSeconds(2));

        var port = manager.GetPort("agwpe");
        Assert.NotNull(port);
        Assert.Equal(2, port.PacketCountReceived);
        Assert.Equal(1, port.PacketCountTransmitted);
        Assert.Equal(Now.AddSeconds(1), port.LastPacketReceivedUtc);
        Assert.Equal(Now.AddSeconds(2), port.LastPacketTransmittedUtc);
    }

    [Fact]
    public void LastErrorIsRecordedAndHealthSummaryReportsIt()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("serial", AprsPortType.SerialKiss));

        manager.RecordError("serial", "port unavailable", Now);

        var port = manager.GetPort("serial");
        var summary = manager.GetHealthSummary();
        Assert.NotNull(port);
        Assert.Equal("port unavailable", port.LastError);
        Assert.Equal(AprsPortConnectionState.Faulted, port.ConnectionState);
        Assert.True(summary.HasErrors);
        Assert.Equal(1, summary.FaultedPorts);
    }

    [Fact]
    public void ClearingCountersWorks()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("tcp", AprsPortType.TcpKiss));
        manager.RecordPacketReceived("tcp", Now);
        manager.RecordPacketTransmitted("tcp", Now);

        var cleared = manager.ClearCounters("tcp");

        var port = manager.GetPort("tcp");
        Assert.True(cleared);
        Assert.NotNull(port);
        Assert.Equal(0, port.PacketCountReceived);
        Assert.Equal(0, port.PacketCountTransmitted);
        Assert.Null(port.LastPacketReceivedUtc);
    }

    [Fact]
    public void DisconnectedPortIsNotTransmitSafe()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("tcp", AprsPortType.TcpKiss) with
        {
            Enabled = true,
            TransmitEnabled = true,
            ConnectionState = AprsPortConnectionState.Disconnected
        });

        var result = manager.CheckTransmitSafety("tcp", globalTransmitSafetyEnabled: true);

        Assert.False(result.IsSafe);
        Assert.Contains("not connected", result.FailureReason);
    }

    [Fact]
    public void RfTransmitRemainsDisabledByDefault()
    {
        var tcp = AprsPortManager.CreateDefaultPort("tcp", "TCP KISS", AprsPortType.TcpKiss, "Network KISS");
        var serial = AprsPortManager.CreateDefaultPort("serial", "Serial KISS", AprsPortType.SerialKiss, "Serial TNC");
        var direwolf = AprsPortManager.CreateDefaultPort("direwolf", "Direwolf", AprsPortType.Direwolf, "Direwolf");
        var agwpe = AprsPortManager.CreateDefaultPort("agwpe", "AGWPE", AprsPortType.Agwpe, "AGWPE");

        Assert.All(new[] { tcp, serial, direwolf, agwpe }, port =>
        {
            Assert.True(AprsPortSourceMapper.IsRfPort(port.PortType));
            Assert.False(port.TransmitEnabled);
        });
    }

    [Fact]
    public void GlobalTransmitSafetyBlocksAllTransmit()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("aprs-is", AprsPortType.AprsIs) with
        {
            Enabled = true,
            TransmitEnabled = true,
            ConnectionState = AprsPortConnectionState.Connected
        });

        var result = manager.CheckTransmitSafety("aprs-is", globalTransmitSafetyEnabled: false);

        Assert.False(result.IsSafe);
        Assert.Contains("Global transmit safety", result.FailureReason);
    }

    [Fact]
    public void StationDatabasePreservesSpecificPacketSourceTags()
    {
        var database = new StationDatabase();
        var parser = new AprsParser();
        var packet = parser.Parse("N0CALL>APRS:!3903.50N/08430.50W-Test beacon", Now);

        database.ProcessPacket(packet, AprsPortSourceMapper.ToPacketSource(AprsPortType.Agwpe));

        var station = database.GetStation("N0CALL");
        Assert.NotNull(station);
        Assert.Equal(AprsPacketSource.Agwpe, station.PacketSource);
    }

    [Fact]
    public void PortStatusViewModelLoadsRowsAndSummary()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(CreatePort("aprs-is", AprsPortType.AprsIs) with { Enabled = true });

        var viewModel = new PortStatusViewModel(manager);

        var row = Assert.Single(viewModel.Ports);
        Assert.Equal("APRS-IS", row.PortName);
        Assert.Contains("1/1 enabled", viewModel.Summary);
        Assert.Equal("No port errors recorded.", viewModel.Health);
    }

    private static AprsPortSnapshot CreatePort(string id, AprsPortType portType)
    {
        return AprsPortManager.CreateDefaultPort(id, FormatName(portType), portType, $"{portType} source");
    }

    private static string FormatName(AprsPortType portType)
    {
        return portType switch
        {
            AprsPortType.AprsIs => "APRS-IS",
            AprsPortType.TcpKiss => "TCP KISS",
            AprsPortType.SerialKiss => "Serial KISS",
            AprsPortType.Direwolf => "Direwolf",
            AprsPortType.Agwpe => "AGWPE",
            _ => portType.ToString()
        };
    }
}
