namespace Aprs.Services;

public interface IApplicationEventBus
{
    IDisposable Subscribe(ApplicationEventType eventType, Action<ApplicationEvent> handler);

    IDisposable SubscribeAll(Action<ApplicationEvent> handler);

    void Publish(ApplicationEvent applicationEvent);
}
