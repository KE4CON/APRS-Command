namespace Aprs.Services;

public interface ISimulationService
{
    SimulationConfiguration Configuration { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    void Stop();

    void Pause();

    void Resume();

    void Reset();

    Task<IReadOnlyList<SimulatedAprsPacket>> GenerateNextBatchAsync(CancellationToken cancellationToken = default);

    SimulationStatus GetStatus();

    IReadOnlyList<SimulatedAprsPacket> GetRecentPackets(int? maximumCount = null);
}
