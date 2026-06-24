namespace Aprs.Services;

public interface IAprsEventBus
{
    AprsEventSubscription Subscribe(
        AprsEventType eventType,
        Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler);

    AprsEventSubscription SubscribeAll(
        Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler);

    AprsEventPublishResult Publish(IAprsEvent aprsEvent);

    Task<AprsEventPublishResult> PublishAsync(IAprsEvent aprsEvent, CancellationToken cancellationToken = default);

    IReadOnlyList<IAprsEvent> GetRecentEvents(int? maximumCount = null);

    void ClearHistory();
}
