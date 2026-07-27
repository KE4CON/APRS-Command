using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Runtime;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class AgwpeCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromSettings_EnabledPortAndEnabledConfig_CreatesOneClient()
    {
        var port = new ConnectionPort(
            "bpq", "BPQ32", ConnectionPortType.Agwpe,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForAgwpe(AgwpeConfiguration.Default with { Enabled = true }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_DisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "bpq", "BPQ32", ConnectionPortType.Agwpe,
            Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForAgwpe(AgwpeConfiguration.Default with { Enabled = true }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_ReceiveDisabledPort_IsSkipped()
    {
        var port = new ConnectionPort(
            "bpq", "BPQ32", ConnectionPortType.Agwpe,
            Enabled: true, ReceiveEnabled: false, TransmitEnabled: true,
            PortConfiguration.ForAgwpe(AgwpeConfiguration.Default with { Enabled = true }));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_NonAgwpePortType_IsSkipped()
    {
        var coordinator = CreateCoordinator(ConnectionPort.DefaultAprsIs());

        Assert.Equal(0, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_MissingAgwpeConfig_IsSkipped()
    {
        var port = new ConnectionPort(
            "bpq", "BPQ32", ConnectionPortType.Agwpe,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            Configuration: new PortConfiguration());

        var coordinator = CreateCoordinator(port);

        Assert.Equal(0, coordinator.ClientCount);
    }

    /// <summary>
    /// Regression test for a real bug found while writing these tests: AgwpeCoordinator used to
    /// require AgwpeConfiguration.Enabled to independently be true, but nothing in the Settings UI
    /// (ConnectionPortRowViewModel only ever writes port.Enabled / ReceiveEnabled / TransmitEnabled)
    /// ever set that inner flag, so a port enabled entirely through the UI would silently never
    /// connect. Fixed to match KissTcpCoordinator/SerialKissCoordinator: the port-level Enabled and
    /// ReceiveEnabled flags are authoritative, and the client config's Enabled is forced to true.
    /// </summary>
    [Fact]
    public void CreateFromSettings_PortEnabledRegardlessOfInnerConfigEnabledFlag_CreatesClient()
    {
        var port = new ConnectionPort(
            "bpq", "BPQ32", ConnectionPortType.Agwpe,
            Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
            PortConfiguration.ForAgwpe(AgwpeConfiguration.Default));

        var coordinator = CreateCoordinator(port);

        Assert.Equal(1, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_MultiplePorts_CreatesClientOnlyForEligibleOnes()
    {
        var settings = AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                ConnectionPort.DefaultAprsIs(),
                new ConnectionPort("bpq1", "BPQ32 enabled", ConnectionPortType.Agwpe,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForAgwpe(AgwpeConfiguration.Default with { Enabled = true })),
                new ConnectionPort("bpq2", "BPQ32 port-disabled", ConnectionPortType.Agwpe,
                    Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForAgwpe(AgwpeConfiguration.Default with { Enabled = true })),
                new ConnectionPort("bpq3", "BPQ32 inner-config-not-explicitly-enabled", ConnectionPortType.Agwpe,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForAgwpe(AgwpeConfiguration.Default))
            ])
        };

        var coordinator = AgwpeCoordinator.CreateFromSettings(settings, CreateIngestion());

        Assert.Equal(2, coordinator.ClientCount);
    }

    [Fact]
    public void CreateFromSettings_NoAgwpePorts_ClientCountIsZero()
    {
        var settings = AppSettings.Default;

        var coordinator = AgwpeCoordinator.CreateFromSettings(settings, CreateIngestion());

        Assert.Equal(0, coordinator.ClientCount);
    }

    private static AgwpeCoordinator CreateCoordinator(params ConnectionPort[] ports)
    {
        var settings = AppSettings.Default with { Connections = new ConnectionSettings(ports) };
        return AgwpeCoordinator.CreateFromSettings(settings, CreateIngestion());
    }

    private static AprsIngestionService CreateIngestion() =>
        new(new AprsParser(), new StationDatabase(), new RawPacketLogService(clock: new FakeClock { UtcNow = Now }));

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
