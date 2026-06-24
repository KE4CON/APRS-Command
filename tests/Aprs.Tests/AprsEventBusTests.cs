using Aprs.Services;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public class AprsEventBusTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-10T09:27:51Z");

    [Fact]
    public async Task PublishAsyncDeliversEventToSubscriber()
    {
        var bus = new AprsEventBus();
        IAprsEvent? received = null;
        using var subscription = bus.Subscribe(AprsEventType.StationUpdated, (aprsEvent, _) =>
        {
            received = aprsEvent;
            return ValueTask.FromResult(AprsEventHandlerResult.Handled);
        });
        var eventToPublish = CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station);

        var result = await bus.PublishAsync(eventToPublish);

        Assert.True(result.Success);
        Assert.Equal(1, result.SubscriberCount);
        Assert.Same(eventToPublish, received);
    }

    [Fact]
    public void MultipleSubscribersReceiveSameEvent()
    {
        var bus = new AprsEventBus();
        var count = 0;
        bus.Subscribe(AprsEventType.WeatherUpdated, (aprsEvent, _) =>
        {
            count++;
            return ValueTask.FromResult(AprsEventHandlerResult.Handled);
        });
        bus.SubscribeAll((aprsEvent, _) =>
        {
            count++;
            return ValueTask.FromResult(AprsEventHandlerResult.Handled);
        });

        var result = bus.Publish(CreateEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather));

        Assert.True(result.Success);
        Assert.Equal(2, result.SubscriberCount);
        Assert.Equal(2, count);
    }

    [Fact]
    public void UnsubscribeStopsDelivery()
    {
        var bus = new AprsEventBus();
        var count = 0;
        var subscription = bus.Subscribe(AprsEventType.AlertTriggered, (aprsEvent, _) =>
        {
            count++;
            return ValueTask.FromResult(AprsEventHandlerResult.Handled);
        });

        subscription.Dispose();
        bus.Publish(CreateEvent(AprsEventType.AlertTriggered, AprsEventCategory.Alert));

        Assert.Equal(0, count);
    }

    [Fact]
    public void PublishingWithNoSubscribersDoesNotFail()
    {
        var bus = new AprsEventBus();

        var result = bus.Publish(CreateEvent(AprsEventType.RawPacketReceived, AprsEventCategory.Packet));

        Assert.True(result.Success);
        Assert.Equal(0, result.SubscriberCount);
        Assert.Empty(result.HandlerResults);
    }

    [Fact]
    public void SubscriberExceptionDoesNotCrashEventBus()
    {
        var bus = new AprsEventBus();
        bus.Subscribe(AprsEventType.PortError, (_, _) => throw new InvalidOperationException("subscriber failed"));

        var result = bus.Publish(CreateEvent(AprsEventType.PortError, AprsEventCategory.Port, AprsEventSeverity.Error));

        Assert.False(result.Success);
        var handlerResult = Assert.Single(result.HandlerResults);
        Assert.False(handlerResult.Success);
        Assert.Contains("subscriber failed", handlerResult.ErrorMessage);
    }

    [Fact]
    public void MetadataAndSourceMetadataArePreserved()
    {
        var bus = new AprsEventBus();
        var aprsEvent = CreateEvent(AprsEventType.MessageReceived, AprsEventCategory.Message);

        bus.Publish(aprsEvent);
        var recent = Assert.Single(bus.GetRecentEvents());

        Assert.Equal(aprsEvent.Metadata.EventId, recent.Metadata.EventId);
        Assert.Equal("N0CALL", recent.Metadata.RelatedCallsign);
        Assert.Equal(ExternalSourceType.Simulation, recent.Metadata.SourceMetadata.SourceType);
        Assert.Equal(ExternalTrustLevel.Internal, recent.Metadata.SourceMetadata.TrustLevel);
    }

    [Fact]
    public void RecentHistoryIsLimitedAndNewestFirst()
    {
        var bus = new AprsEventBus(new AprsEventBusConfiguration(2));

        bus.Publish(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station, timestamp: Now));
        bus.Publish(CreateEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather, timestamp: Now.AddSeconds(1)));
        bus.Publish(CreateEvent(AprsEventType.GpsUpdated, AprsEventCategory.GPS, timestamp: Now.AddSeconds(2)));

        var recent = bus.GetRecentEvents();

        Assert.Equal(2, recent.Count);
        Assert.Equal(AprsEventType.GpsUpdated, recent[0].Metadata.EventType);
        Assert.Equal(AprsEventType.WeatherUpdated, recent[1].Metadata.EventType);
    }

    [Fact]
    public void DecodedEventLogPublishesToEventBus()
    {
        var bus = new AprsEventBus();
        var service = new DecodedEventLogService(clock: new FakeClock { UtcNow = Now }, eventBus: bus);

        service.AddStationEvent(DecodedEventType.StationUpdated, "N0CALL", "Station updated.", AprsPacketSource.Simulation);

        var aprsEvent = Assert.Single(bus.GetRecentEvents());
        Assert.Equal(AprsEventType.StationUpdated, aprsEvent.Metadata.EventType);
        Assert.Equal(AprsEventCategory.Station, aprsEvent.Metadata.EventCategory);
        Assert.Equal(ExternalSourceType.Simulation, aprsEvent.Metadata.SourceMetadata.SourceType);
    }

    [Fact]
    public void PublishingEventsDoesNotCallTransmitServices()
    {
        var bus = new AprsEventBus();
        var transmit = new FakeTransmitServices();

        bus.Publish(CreateEvent(AprsEventType.PacketTransmitRequested, AprsEventCategory.Packet));

        Assert.Equal(0, transmit.AprsIsTransmitCalls);
        Assert.Equal(0, transmit.RfTransmitCalls);
    }

    private static IAprsEvent CreateEvent(
        AprsEventType eventType,
        AprsEventCategory category,
        AprsEventSeverity severity = AprsEventSeverity.Info,
        DateTimeOffset? timestamp = null)
    {
        var eventTime = timestamp ?? Now;
        var source = new ExternalSourceMetadata(
            "Simulation",
            ExternalSourceType.Simulation,
            "sim",
            eventTime,
            ContractDataOrigin.Simulated,
            ExternalTrustLevel.Internal);
        var metadata = AprsEventMetadata.Create(
            eventType,
            category,
            eventTime,
            source,
            severity,
            relatedCallsign: "N0CALL",
            summary: $"{eventType} summary");

        return new AprsEventEnvelope<string>(metadata, "payload");
    }

    private sealed class FakeTransmitServices
    {
        public int AprsIsTransmitCalls { get; private set; }

        public int RfTransmitCalls { get; private set; }

        public void TransmitAprsIs()
        {
            AprsIsTransmitCalls++;
        }

        public void TransmitRf()
        {
            RfTransmitCalls++;
        }
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; init; }
    }
}
