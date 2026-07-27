using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class SerialKissCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromSettings_EnabledSerialKissPortWithPortName_CreatesOneClient()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_DisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_ReceiveDisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: false, TransmitEnabled: true,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_NonSerialKissPortType_IsSkipped()
    {
        var coordinator = CreateCoordinator(ConnectionPort.DefaultAprsIs());

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_NetworkTncKissPortType_IsNotTreatedAsSerial()
    {
        var port = new ConnectionPort(
            "gray-wolf", "GrayWolf", ConnectionPortType.NetworkTncKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_MissingSerialKissConfig_IsSkipped()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: new PortConfiguration());

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_BlankPortName_IsSkipped()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_WhitespaceOnlyPortName_IsSkipped()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "   " }));

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
                new ConnectionPort("tnc1", "Hardware TNC 1", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" })),
                new ConnectionPort("tnc2", "Disabled TNC", ConnectionPortType.SerialKiss,
                    Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM4" })),
                new ConnectionPort("tnc3", "No port selected", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default))
            ])
        };

        var coordinator = SerialKissCoordinator.CreateFromSettings(settings, CreateIngestion());

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void GetTransmitClients_OnlyReturnsTransmitEnabledClients()
    {
        var settings = AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                new ConnectionPort("rx-only", "Receive only", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" })),
                new ConnectionPort("tx-enabled", "Transmit enabled", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM4" }))
            ])
        };

        var coordinator = SerialKissCoordinator.CreateFromSettings(settings, CreateIngestion());

        var transmitClients = coordinator.GetTransmitClients();

        var client = Assert.Single(transmitClients);
        Assert.True(client.Configuration.TransmitEnabled);
        Assert.Equal("COM4", client.Configuration.PortName);
    }

    [Fact]
    public void GetTransmitClients_NoTransmitEnabledPorts_ReturnsEmpty()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" }));

        var coordinator = CreateCoordinator(port);

        Assert.Empty(coordinator.GetTransmitClients());
    }

    [Fact]
    public void CreateFromSettings_PropagatesPortEnableFlagsIntoClientConfiguration()
    {
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
            PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with
            {
                PortName = "/dev/ttyUSB0",
                BaudRate = 19200
            }));

        var coordinator = CreateCoordinator(port);
        var client = Assert.Single(coordinator.GetTransmitClients());

        Assert.True(client.Configuration.Enabled);
        Assert.True(client.Configuration.ReceiveEnabled);
        Assert.True(client.Configuration.TransmitEnabled);
        Assert.Equal(19200, client.Configuration.BaudRate);
        Assert.Contains("/dev/ttyUSB0", client.Configuration.SourceName);
    }

    private static SerialKissCoordinator CreateCoordinator(params ConnectionPort[] ports)
    {
        var settings = AppSettings.Default with { Connections = new ConnectionSettings(ports) };
        return SerialKissCoordinator.CreateFromSettings(settings, CreateIngestion());
    }

    private static AprsIngestionService CreateIngestion() =>
        new(new AprsParser(), new StationDatabase(), new RawPacketLogService(clock: new FakeClock { UtcNow = Now }));

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
