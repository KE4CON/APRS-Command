namespace Aprs.Services;

/// <summary>
/// Receives generated simulation packets for normal parser/log/station/weather/object pipelines.
/// </summary>
public interface ISimulatedAprsPacketSink
{
    Task PublishSimulatedPacketAsync(SimulatedAprsPacket packet, CancellationToken cancellationToken = default);
}
