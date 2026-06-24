namespace Aprs.Services;

public sealed class NoOpSimulatedAprsPacketSink : ISimulatedAprsPacketSink
{
    public Task PublishSimulatedPacketAsync(SimulatedAprsPacket packet, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
