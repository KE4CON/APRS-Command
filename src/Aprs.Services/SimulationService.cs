namespace Aprs.Services;

public sealed class SimulationService : ISimulationService
{
    private readonly ISimulatedAprsPacketSink sink;
    private readonly ISimulatedAprsPacketGenerator generator;
    private readonly IBeaconSchedulerClock clock;
    private readonly List<SimulatedAprsPacket> recentPackets = [];
    private readonly List<SimulatedMobileStation> mobileStations = [];
    private SimulationState state = SimulationState.Stopped;
    private int sequence;
    private DateTimeOffset? lastGeneratedAtUtc;
    private string? lastError;

    public SimulationService(
        ISimulatedAprsPacketSink sink,
        SimulationConfiguration? configuration = null,
        ISimulatedAprsPacketGenerator? generator = null,
        IBeaconSchedulerClock? clock = null)
    {
        this.sink = sink;
        Configuration = Normalize(configuration ?? SimulationConfiguration.Default);
        this.generator = generator ?? new SimulatedAprsPacketGenerator();
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        InitializeMobiles();
    }

    public SimulationConfiguration Configuration { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!Configuration.SimulationEnabled)
        {
            state = SimulationState.Stopped;
            return;
        }

        state = SimulationState.Starting;
        try
        {
            state = SimulationState.Running;
            await GenerateNextBatchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            state = SimulationState.Stopped;
            throw;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            state = SimulationState.Faulted;
        }
    }

    public void Stop()
    {
        state = SimulationState.Stopped;
    }

    public void Pause()
    {
        if (state == SimulationState.Running)
        {
            state = SimulationState.Paused;
        }
    }

    public void Resume()
    {
        if (state == SimulationState.Paused)
        {
            state = SimulationState.Running;
        }
    }

    public void Reset()
    {
        recentPackets.Clear();
        sequence = 0;
        lastGeneratedAtUtc = null;
        lastError = null;
        state = SimulationState.Stopped;
        InitializeMobiles();
    }

    public async Task<IReadOnlyList<SimulatedAprsPacket>> GenerateNextBatchAsync(CancellationToken cancellationToken = default)
    {
        if (state is SimulationState.Stopped or SimulationState.Paused or SimulationState.Faulted || !Configuration.SimulationEnabled)
        {
            return [];
        }

        var now = clock.UtcNow;
        var elapsed = lastGeneratedAtUtc is null ? Configuration.UpdateInterval : now - lastGeneratedAtUtc.Value;
        var generated = new List<SimulatedAprsPacket>();

        for (var i = 1; i <= Configuration.FixedStationCount; i++)
        {
            generated.Add(CreatePacket(generator.GenerateFixedStationPacket(i, Configuration), now, "FixedStation", $"SIM{i:000}"));
            generated.Add(CreatePacket(generator.GenerateStatusPacket(i), now, "Status", $"SIM{i:000}"));
        }

        for (var i = 0; i < mobileStations.Count; i++)
        {
            mobileStations[i] = generator.UpdateMobileStation(mobileStations[i], elapsed, Configuration);
            generated.Add(CreatePacket(generator.GenerateMobileStationPacket(mobileStations[i]), now, "MobileStation", mobileStations[i].Callsign));
        }

        for (var i = 1; i <= Configuration.WeatherStationCount; i++)
        {
            generated.Add(CreatePacket(generator.GenerateWeatherStationPacket(i, Configuration), now, "Weather", $"TESTWX{i}"));
        }

        for (var i = 1; i <= Configuration.ObjectCount; i++)
        {
            generated.Add(CreatePacket(generator.GenerateObjectPacket(i, Configuration), now, "Object", $"OBJTEST{i}"));
        }

        if (Configuration.GenerateMessages)
        {
            generated.Add(CreatePacket(generator.GenerateMessagePacket(++sequence), now, "Message", "N0CALL"));
        }

        if (Configuration.GenerateBulletins)
        {
            generated.Add(CreatePacket(generator.GenerateBulletinPacket(sequence), now, "Bulletin", "BLN"));
        }

        foreach (var packet in generated)
        {
            recentPackets.Add(packet);
            await sink.PublishSimulatedPacketAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        lastGeneratedAtUtc = now;
        return generated.ToArray();
    }

    public SimulationStatus GetStatus()
    {
        return new SimulationStatus(
            state,
            Configuration.SimulationEnabled,
            Configuration.TransmitDisabled,
            Configuration.SimulationSourceName,
            recentPackets.Count,
            lastGeneratedAtUtc,
            lastError);
    }

    public IReadOnlyList<SimulatedAprsPacket> GetRecentPackets(int? maximumCount = null)
    {
        var query = recentPackets.OrderByDescending(packet => packet.GeneratedAtUtc).ThenByDescending(packet => packet.PacketId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    private void InitializeMobiles()
    {
        mobileStations.Clear();
        for (var i = 1; i <= Configuration.MobileStationCount; i++)
        {
            mobileStations.Add(generator.CreateMobileStation(i, Configuration));
        }
    }

    private SimulatedAprsPacket CreatePacket(string rawPacket, DateTimeOffset timestamp, string packetKind, string? entityName)
    {
        return new SimulatedAprsPacket(Guid.NewGuid(), rawPacket, AprsPacketSource.Simulation, timestamp, Configuration.SimulationSourceName, packetKind, entityName);
    }

    private static SimulationConfiguration Normalize(SimulationConfiguration configuration)
    {
        return configuration with
        {
            FixedStationCount = Math.Max(0, configuration.FixedStationCount),
            MobileStationCount = Math.Max(0, configuration.MobileStationCount),
            WeatherStationCount = Math.Max(0, configuration.WeatherStationCount),
            ObjectCount = Math.Max(0, configuration.ObjectCount),
            UpdateInterval = configuration.UpdateInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : configuration.UpdateInterval,
            MaximumSimulatedSpeedKnots = Math.Max(0, configuration.MaximumSimulatedSpeedKnots),
            AreaRadiusMeters = Math.Max(100, configuration.AreaRadiusMeters)
        };
    }
}
