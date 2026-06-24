namespace Aprs.Services;

public sealed class AprsEventSubscription : IDisposable
{
    private readonly Action dispose;
    private bool disposed;

    internal AprsEventSubscription(Guid subscriptionId, AprsEventType? eventType, Action dispose)
    {
        SubscriptionId = subscriptionId;
        EventType = eventType;
        this.dispose = dispose;
    }

    public Guid SubscriptionId { get; }

    public AprsEventType? EventType { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        dispose();
    }
}
