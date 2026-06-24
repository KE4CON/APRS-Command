namespace Aprs.Services;

public sealed class AprsEventBus : IAprsEventBus
{
    private readonly object syncRoot = new();
    private readonly AprsEventBusConfiguration configuration;
    private readonly Dictionary<AprsEventType, List<Subscriber>> typedSubscribers = [];
    private readonly List<Subscriber> allSubscribers = [];
    private readonly List<IAprsEvent> recentEvents = [];

    public AprsEventBus(AprsEventBusConfiguration? configuration = null)
    {
        this.configuration = configuration ?? AprsEventBusConfiguration.Default;
    }

    public AprsEventSubscription Subscribe(
        AprsEventType eventType,
        Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscriber = new Subscriber(Guid.NewGuid(), handler);
        lock (syncRoot)
        {
            if (!typedSubscribers.TryGetValue(eventType, out var subscribers))
            {
                subscribers = [];
                typedSubscribers[eventType] = subscribers;
            }

            subscribers.Add(subscriber);
        }

        return new AprsEventSubscription(subscriber.Id, eventType, () => Unsubscribe(eventType, subscriber.Id));
    }

    public AprsEventSubscription SubscribeAll(
        Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscriber = new Subscriber(Guid.NewGuid(), handler);
        lock (syncRoot)
        {
            allSubscribers.Add(subscriber);
        }

        return new AprsEventSubscription(subscriber.Id, null, () => UnsubscribeAll(subscriber.Id));
    }

    public AprsEventPublishResult Publish(IAprsEvent aprsEvent)
    {
        return PublishAsync(aprsEvent).GetAwaiter().GetResult();
    }

    public async Task<AprsEventPublishResult> PublishAsync(IAprsEvent aprsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aprsEvent);

        Subscriber[] subscribers;
        lock (syncRoot)
        {
            AddToHistory(aprsEvent);
            var typed = typedSubscribers.TryGetValue(aprsEvent.Metadata.EventType, out var currentTyped)
                ? currentTyped
                : [];
            subscribers = allSubscribers.Concat(typed).ToArray();
        }

        var results = new List<AprsEventHandlerResult>(subscribers.Length);
        foreach (var subscriber in subscribers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(AprsEventHandlerResult.Failed("Event publishing was cancelled."));
                break;
            }

            try
            {
                var result = await subscriber.Handler(aprsEvent, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(AprsEventHandlerResult.Failed(ex.Message, ex));
            }
        }

        return new AprsEventPublishResult(aprsEvent, subscribers.Length, results);
    }

    public IReadOnlyList<IAprsEvent> GetRecentEvents(int? maximumCount = null)
    {
        lock (syncRoot)
        {
            var events = recentEvents
                .OrderByDescending(aprsEvent => aprsEvent.Metadata.TimestampUtc)
                .ThenByDescending(aprsEvent => aprsEvent.Metadata.EventId)
                .ToArray();

            return maximumCount is > 0
                ? events.Take(maximumCount.Value).ToArray()
                : events;
        }
    }

    public void ClearHistory()
    {
        lock (syncRoot)
        {
            recentEvents.Clear();
        }
    }

    private void AddToHistory(IAprsEvent aprsEvent)
    {
        if (configuration.MaximumRecentEvents <= 0)
        {
            return;
        }

        recentEvents.Add(aprsEvent);
        while (recentEvents.Count > configuration.MaximumRecentEvents)
        {
            recentEvents.RemoveAt(0);
        }
    }

    private void Unsubscribe(AprsEventType eventType, Guid subscriberId)
    {
        lock (syncRoot)
        {
            if (!typedSubscribers.TryGetValue(eventType, out var subscribers))
            {
                return;
            }

            subscribers.RemoveAll(subscriber => subscriber.Id == subscriberId);
            if (subscribers.Count == 0)
            {
                typedSubscribers.Remove(eventType);
            }
        }
    }

    private void UnsubscribeAll(Guid subscriberId)
    {
        lock (syncRoot)
        {
            allSubscribers.RemoveAll(subscriber => subscriber.Id == subscriberId);
        }
    }

    private sealed record Subscriber(
        Guid Id,
        Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> Handler);
}
