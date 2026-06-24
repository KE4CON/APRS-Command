namespace Aprs.Services;

/// <summary>
/// Receives replayed packet lines for downstream station, map, message, weather, object, and log pipelines.
/// </summary>
public interface IReplayPacketSink
{
    Task PublishReplayPacketAsync(ReplayPacketDispatch dispatch, CancellationToken cancellationToken = default);
}
