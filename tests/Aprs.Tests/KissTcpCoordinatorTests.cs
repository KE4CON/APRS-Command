using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class KissTcpCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromSettings_EnabledNetworkTncKissPort_CreatesOneClient()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_DisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_ReceiveDisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: false, TransmitEnabled: true,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_NonKissPortType_IsSkipped()
    {
        var coordinator = CreateCoordinator(ConnectionPort.DefaultAprsIs());

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_ManagedLocalModemPort_IsIncluded()
    {
        var port = new ConnectionPort(
            "modem", "Local modem", ConnectionPortType.ManagedLocalModem,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_MissingNetworkTncKissConfig_IsSkipped()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: new PortConfiguration());

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_MultiplePorts_CreatesClientOnlyForEligibleOnes()
    {
        var settings = AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                ConnectionPort.DefaultAprsIs(),
                new ConnectionPort("gw1", "GrayWolf", ConnectionPortType.NetworkTncKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default)),
                new ConnectionPort("gw2", "Disabled GrayWolf", ConnectionPortType.NetworkTncKiss,
                    Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default)),
                new ConnectionPort("modem", "Local modem", ConnectionPortType.ManagedLocalModem,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default))
            ])
        };

        var coordinator = KissTcpCoordinator.CreateFromSettings(settings, CreateIngestion());

        Assert.Equal(2, coordinator.ClientCount);
    }

    [Fact]
    public void GetTransmitClients_OnlyReturnsTransmitEnabledClients()
    {
        var settings = AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                new ConnectionPort("rx-only", "Receive only", ConnectionPortType.NetworkTncKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default with { Port = 8001 })),
                new ConnectionPort("tx-enabled", "Transmit enabled", ConnectionPortType.NetworkTncKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default with { Port = 8002 }))
            ])
        };

        var coordinator = KissTcpCoordinator.CreateFromSettings(settings, CreateIngestion());

        var transmitClients = coordinator.GetTransmitClients();

        var client = Assert.Single(transmitClients);
        Assert.True(client.Configuration.TransmitEnabled);
        Assert.Equal(8002, client.Configuration.Port);
    }

    [Fact]
    public void GetTransmitClients_NoTransmitEnabledPorts_ReturnsEmpty()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Empty(coordinator.GetTransmitClients());
    }

    [Fact]
    public void CreateFromSettings_PropagatesPortEnableFlagsIntoClientConfiguration()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default with
            {
                Host = "192.168.1.50",
                Port = 8001
            }));

        var coordinator = CreateCoordinator(port);
        var client = Assert.Single(coordinator.GetTransmitClients());

        Assert.True(client.Configuration.Enabled);
        Assert.True(client.Configuration.ReceiveEnabled);
        Assert.True(client.Configuration.TransmitEnabled);
        Assert.Contains("192.168.1.50", client.Configuration.SourceName);
        Assert.Contains("8001", client.Configuration.SourceName);
    }

    private static KissTcpCoordinator CreateCoordinator(params ConnectionPort[] ports)
    {
        var settings = AppSettings.Default with { Connections = new ConnectionSettings(ports) };
        return KissTcpCoordinator.CreateFromSettings(settings, CreateIngestion());
    }

    private static AprsIngestionService CreateIngestion() =>
        new(new AprsParser(), new StationDatabase(), new RawPacketLogService(clock: new FakeClock { UtcNow = Now }));

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
