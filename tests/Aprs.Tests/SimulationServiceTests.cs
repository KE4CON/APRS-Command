using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class SimulationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly AprsParser parser = new();

    [Fact]
    public void SimulationIsDisabledByDefaultAndTransmitDisabled()
    {
        var configuration = SimulationConfiguration.Default;

        Assert.False(configuration.SimulationEnabled);
        Assert.True(configuration.TransmitDisabled);
    }

    [Fact]
    public void FixedStationPacketGenerationWorks()
    {
        var packet = new SimulatedAprsPacketGenerator().GenerateFixedStationPacket(1, EnabledConfiguration());

        Assert.StartsWith("SIM001>APRS:", packet);
        AssertParses(packet);
    }

    [Fact]
    public void MobileStationPacketGenerationWorks()
    {
        var generator = new SimulatedAprsPacketGenerator();
        var station = generator.CreateMobileStation(1, EnabledConfiguration());

        var packet = generator.GenerateMobileStationPacket(station);

        Assert.Contains("-9>APRS:", packet);
        AssertParses(packet);
    }

    [Fact]
    public void WeatherStationPacketGenerationWorks()
    {
        var packet = new SimulatedAprsPacketGenerator().GenerateWeatherStationPacket(1, EnabledConfiguration());

        Assert.StartsWith("TESTWX1>APRS:", packet);
        AssertParses(packet);
    }

    [Fact]
    public void ObjectPacketGenerationWorks()
    {
        var packet = new SimulatedAprsPacketGenerator().GenerateObjectPacket(1, EnabledConfiguration());

        Assert.Contains(";OBJTEST1 *", packet);
        AssertParses(packet);
    }

    [Fact]
    public void MessagesAndBulletinsParseSuccessfully()
    {
        var generator = new SimulatedAprsPacketGenerator();

        AssertParses(generator.GenerateMessagePacket(1));
        AssertParses(generator.GenerateBulletinPacket(1));
    }

    [Fact]
    public async Task SimulatedPacketsAreTaggedAsSimulation()
    {
        var sink = new FakeSimulationSink();
        var service = CreateService(sink);

        await service.StartAsync();

        Assert.NotEmpty(sink.Packets);
        Assert.All(sink.Packets, packet => Assert.Equal(AprsPacketSource.Simulation, packet.PacketSource));
    }

    [Fact]
    public void MobileStationMovementChangesPosition()
    {
        var generator = new SimulatedAprsPacketGenerator();
        var configuration = EnabledConfiguration();
        var station = generator.CreateMobileStation(1, configuration);

        var moved = generator.UpdateMobileStation(station, TimeSpan.FromMinutes(5), configuration);

        Assert.NotEqual(station.Latitude, moved.Latitude);
        Assert.NotEqual(station.Longitude, moved.Longitude);
    }

    [Fact]
    public async Task SimulationStartStopStateChangesCorrectly()
    {
        var service = CreateService();

        await service.StartAsync();
        Assert.Equal(SimulationState.Running, service.GetStatus().State);

        service.Stop();
        Assert.Equal(SimulationState.Stopped, service.GetStatus().State);
    }

    [Fact]
    public async Task PauseResumeStateChangesCorrectly()
    {
        var service = CreateService();
        await service.StartAsync();

        service.Pause();
        Assert.Equal(SimulationState.Paused, service.GetStatus().State);

        service.Resume();
        Assert.Equal(SimulationState.Running, service.GetStatus().State);
    }

    [Fact]
    public async Task SimulatedPacketPipelineDoesNotCallTransmit()
    {
        var sink = new FakeSimulationSink();
        var service = CreateService(sink);

        await service.StartAsync();

        Assert.Equal(0, sink.AprsIsTransmitCallCount);
        Assert.Equal(0, sink.RfTransmitCallCount);
        Assert.Equal(0, sink.IGateTransmitCallCount);
        Assert.Equal(0, sink.DigipeaterTransmitCallCount);
        Assert.Equal(0, sink.BeaconTransmitCallCount);
    }

    [Fact]
    public async Task DisabledSimulationDoesNotGeneratePackets()
    {
        var sink = new FakeSimulationSink();
        var service = new SimulationService(sink, SimulationConfiguration.Default, clock: new FakeClock { UtcNow = Now });

        await service.StartAsync();

        Assert.Empty(sink.Packets);
        Assert.Equal(SimulationState.Stopped, service.GetStatus().State);
    }

    private void AssertParses(string packet)
    {
        Assert.True(parser.TryParse(packet, Now, out var parsed, out var error), error);
        Assert.NotNull(parsed);
    }

    private static SimulationService CreateService(FakeSimulationSink? sink = null)
    {
        return new SimulationService(sink ?? new FakeSimulationSink(), EnabledConfiguration(), clock: new FakeClock { UtcNow = Now });
    }

    private static SimulationConfiguration EnabledConfiguration()
    {
        return SimulationConfiguration.Default with
        {
            SimulationEnabled = true,
            FixedStationCount = 1,
            MobileStationCount = 1,
            WeatherStationCount = 1,
            ObjectCount = 1,
            GenerateMessages = true,
            GenerateBulletins = true,
            UpdateInterval = TimeSpan.FromSeconds(30)
        };
    }

    private sealed class FakeSimulationSink : ISimulatedAprsPacketSink
    {
        public List<SimulatedAprsPacket> Packets { get; } = [];

        public int AprsIsTransmitCallCount { get; }

        public int RfTransmitCallCount { get; }

        public int IGateTransmitCallCount { get; }

        public int DigipeaterTransmitCallCount { get; }

        public int BeaconTransmitCallCount { get; }

        public Task PublishSimulatedPacketAsync(SimulatedAprsPacket packet, CancellationToken cancellationToken = default)
        {
            Packets.Add(packet);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
