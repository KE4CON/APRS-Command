namespace Aprs.Services;

public sealed class NoOpReplayPacketSink : IReplayPacketSink
{
    public Task PublishReplayPacketAsync(ReplayPacketDispatch dispatch, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
