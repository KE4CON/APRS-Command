using Aprs.Desktop.Configuration;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for the connection port-list model: the default port, multi-port support, and the
/// normalization that fills in each port's type-specific configuration.
/// </summary>
public sealed class ConnectionSettingsTests
{
    [Fact]
    public void Default_IsSingleReceiveOnlyAprsIsPort()
    {
        var port = Assert.Single(ConnectionSettings.Default.Ports);

        Assert.Equal(ConnectionPortType.AprsIs, port.Type);
        Assert.True(port.Enabled);
        Assert.True(port.ReceiveEnabled);
        Assert.False(port.TransmitEnabled); // never transmit by default
        Assert.NotNull(port.Configuration.AprsIs);
    }

    [Fact]
    public void Normalized_EmptyList_FallsBackToDefault()
    {
        var normalized = new ConnectionSettings([]).Normalized();

        var port = Assert.Single(normalized.Ports);
        Assert.Equal(ConnectionPortType.AprsIs, port.Type);
    }

    [Fact]
    public void Normalized_FillsMissingTypeConfiguration()
    {
        // A serial port saved with no configuration gets the serial default filled in.
        var port = new ConnectionPort(
            "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: new PortConfiguration());

        var normalized = new ConnectionSettings([port]).Normalized();

        Assert.NotNull(normalized.Ports[0].Configuration.SerialKiss);
    }

    [Fact]
    public void Normalized_ManagedLocalModem_UsesLoopbackKissConfiguration()
    {
        var port = new ConnectionPort(
            "modem", "Local modem", ConnectionPortType.ManagedLocalModem,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: new PortConfiguration());

        var normalized = port.Normalized();

        // Until dedicated audio/PTT settings exist, a managed modem connects over loopback KISS-TCP.
        Assert.NotNull(normalized.Configuration.NetworkTncKiss);
    }

    [Fact]
    public void MultiplePorts_OfDifferentTypes_AreAllowed()
    {
        var settings = new ConnectionSettings(
        [
            ConnectionPort.DefaultAprsIs(),
            new ConnectionPort("rf1", "RF 1", ConnectionPortType.SerialKiss,
                Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
                PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default)),
            new ConnectionPort("rf2", "RF 2", ConnectionPortType.NetworkTncKiss,
                Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default))
        ]);

        var normalized = settings.Normalized();

        Assert.Equal(3, normalized.Ports.Count);
        Assert.Contains(normalized.Ports, p => p.Type == ConnectionPortType.AprsIs);
        Assert.Contains(normalized.Ports, p => p.Type == ConnectionPortType.SerialKiss);
        Assert.Contains(normalized.Ports, p => p.Type == ConnectionPortType.NetworkTncKiss);
    }

    [Fact]
    public void DefaultAprsIsPort_NeverTransmitsByDefault()
    {
        Assert.False(ConnectionPort.DefaultAprsIs().TransmitEnabled);
    }
}
