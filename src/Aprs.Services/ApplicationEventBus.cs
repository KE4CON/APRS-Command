namespace Aprs.Services;

public sealed class ApplicationEventBus : IApplicationEventBus
{
    private readonly object syncRoot = new();
    private readonly Dictionary<ApplicationEventType, List<Action<ApplicationEvent>>> typedHandlers = [];
    private readonly List<Action<ApplicationEvent>> allHandlers = [];

    public IDisposable Subscribe(ApplicationEventType eventType, Action<ApplicationEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (syncRoot)
        {
            if (!typedHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = [];
                typedHandlers[eventType] = handlers;
            }

            handlers.Add(handler);
        }

        return new Subscription(() => Unsubscribe(eventType, handler));
    }

    public IDisposable SubscribeAll(Action<ApplicationEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (syncRoot)
        {
            allHandlers.Add(handler);
        }

        return new Subscription(() => UnsubscribeAll(handler));
    }

    public void Publish(ApplicationEvent applicationEvent)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        Action<ApplicationEvent>[] handlers;
        lock (syncRoot)
        {
            var typed = typedHandlers.TryGetValue(applicationEvent.EventType, out var currentTyped)
                ? currentTyped
                : [];
            handlers = allHandlers.Concat(typed).ToArray();
        }

        foreach (var handler in handlers)
        {
            handler(applicationEvent);
        }
    }

    private void Unsubscribe(ApplicationEventType eventType, Action<ApplicationEvent> handler)
    {
        lock (syncRoot)
        {
            if (typedHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    typedHandlers.Remove(eventType);
                }
            }
        }
    }

    private void UnsubscribeAll(Action<ApplicationEvent> handler)
    {
        lock (syncRoot)
        {
            allHandlers.Remove(handler);
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            unsubscribe();
        }
    }
}
