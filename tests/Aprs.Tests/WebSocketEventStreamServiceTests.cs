using System.Text.Json;
using Aprs.Services;
using AprsCommand.Api;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public class WebSocketEventStreamServiceTests
{
    private const string Token = "test-token";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-12T10:15:30Z");

    [Fact]
    public void ConfigurationDefaultsAreSafe()
    {
        var configuration = WebSocketEventStreamConfiguration.Default;

        Assert.False(configuration.WebSocketEnabled);
        Assert.True(configuration.LocalhostOnly);
        Assert.Equal("127.0.0.1", configuration.BindAddress);
        Assert.True(configuration.RequireToken);
        Assert.True(configuration.ReadOnlyStreamingOnly);
        Assert.False(configuration.HasTransmitCapability);
        Assert.True(configuration.MaximumConnectedClients > 0);
        Assert.True(configuration.MaximumEventsPerSecondPerClient > 0);
    }

    [Fact]
    public async Task StartDoesNotRunWhenDisabledByDefault()
    {
        var service = new WebSocketEventStreamService();

        var status = await service.StartAsync();

        Assert.Equal(WebSocketEventStreamState.Stopped, status.State);
        Assert.Contains("disabled", status.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnauthorizedClientIsRejected()
    {
        var service = CreateService();
        await service.StartAsync();

        var missing = await service.ConnectClientAsync(new WebSocketEventStreamClientRequest("/ws/events"), new FakeClient("missing"));
        var invalid = await service.ConnectClientAsync(new WebSocketEventStreamClientRequest("/ws/events", Token: "wrong"), new FakeClient("invalid"));
        var nonLocalhost = await service.ConnectClientAsync(
            new WebSocketEventStreamClientRequest("/ws/events", RemoteAddress: "192.0.2.55", Token: Token),
            new FakeClient("remote"));

        Assert.Equal(401, missing.StatusCode);
        Assert.Equal(401, invalid.StatusCode);
        Assert.Equal(403, nonLocalhost.StatusCode);
    }

    [Fact]
    public async Task AuthorizedClientCanConnect()
    {
        var service = CreateService();
        await service.StartAsync();
        var client = new FakeClient("client-1");

        var result = await service.ConnectClientAsync(Connect("/ws/events"), client);

        Assert.True(result.Success);
        Assert.Equal("client-1", result.ClientId);
        Assert.Single(service.ConnectedClients);
    }

    [Fact]
    public void EnvelopeSerializesToJson()
    {
        var service = CreateService();
        var envelope = service.ToEnvelope(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station));

        var json = JsonSerializer.Serialize(envelope, ContractJsonSerializerOptions.Create());

        Assert.Contains("\"schemaVersion\":\"1.0\"", json);
        Assert.Contains("\"eventType\":\"StationUpdated\"", json);
        Assert.Contains("\"payloadType\":\"StationUpdateDto\"", json);
    }

    [Fact]
    public void EventBusEventConvertsToPublicEnvelope()
    {
        var service = CreateService();

        var envelope = service.ToEnvelope(CreateEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather));

        Assert.Equal("WeatherUpdated", envelope.EventType);
        Assert.Equal("Weather", envelope.EventCategory);
        Assert.Equal(nameof(WeatherObservationDto), envelope.PayloadType);
        var payload = Assert.IsType<WeatherObservationDto>(envelope.Payload);
        Assert.Equal("N0CALL", payload.Callsign);
        Assert.Equal(ExternalSourceType.Simulation, envelope.SourceMetadata.SourceType);
    }

    [Fact]
    public async Task EventBusBroadcastReachesConnectedFakeClient()
    {
        var bus = new AprsEventBus();
        var service = CreateService(eventBus: bus);
        var client = new FakeClient("client-1");
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/events"), client);

        bus.Publish(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station));

        var envelope = Assert.Single(client.Sent);
        Assert.Equal("StationUpdated", envelope.EventType);
        Assert.Equal(nameof(StationUpdateDto), envelope.PayloadType);
    }

    [Fact]
    public async Task DisconnectedClientDoesNotCrashBroadcast()
    {
        var service = CreateService();
        var client = new FakeClient("client-1") { IsConnected = false };
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/events"), client);

        var sent = await service.BroadcastAsync(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station));

        Assert.Equal(0, sent);
        Assert.Empty(service.ConnectedClients);
    }

    [Fact]
    public async Task FailingClientIsDisconnectedSafely()
    {
        var service = CreateService();
        var client = new FakeClient("client-1") { ThrowOnSend = true };
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/events"), client);

        var sent = await service.BroadcastAsync(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station));

        Assert.Equal(0, sent);
        Assert.Empty(service.ConnectedClients);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task UnknownInboundCommandIsRejected()
    {
        var service = CreateService();
        var client = new FakeClient("client-1");
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/events"), client);

        var result = await service.HandleInboundMessageAsync("client-1", new WebSocketInboundMessage("transmit"));

        Assert.False(result.Success);
        Assert.Contains("Unknown", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientFilterLimitsCategories()
    {
        var service = CreateService();
        var client = new FakeClient("client-1");
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/stations"), client);

        await service.BroadcastAsync(CreateEvent(AprsEventType.WeatherUpdated, AprsEventCategory.Weather));
        await service.BroadcastAsync(CreateEvent(AprsEventType.StationUpdated, AprsEventCategory.Station));

        var envelope = Assert.Single(client.Sent);
        Assert.Equal("StationUpdated", envelope.EventType);
    }

    [Fact]
    public async Task RawPacketFlagCanSuppressRawPacketEvents()
    {
        var service = CreateService(configuration: EnabledConfiguration() with { IncludeRawPackets = false });
        var client = new FakeClient("client-1");
        await service.StartAsync();
        await service.ConnectClientAsync(Connect("/ws/events"), client);

        var sent = await service.BroadcastAsync(CreateEvent(AprsEventType.RawPacketReceived, AprsEventCategory.Packet));

        Assert.Equal(0, sent);
        Assert.Empty(client.Sent);
    }

    [Fact]
    public async Task WebSocketServiceDoesNotCallTransmitServices()
    {
        var transmit = new FakeTransmitServices();
        var service = CreateService();
        await service.StartAsync();

        await service.BroadcastAsync(CreateEvent(AprsEventType.PacketTransmitBlocked, AprsEventCategory.Packet, AprsEventSeverity.Warning));
        await service.HandleInboundMessageAsync("missing", new WebSocketInboundMessage("transmit"));

        Assert.Equal(0, transmit.AprsIsTransmitCalls);
        Assert.Equal(0, transmit.RfTransmitCalls);
    }

    private static WebSocketEventStreamService CreateService(
        WebSocketEventStreamConfiguration? configuration = null,
        IAprsEventBus? eventBus = null)
    {
        return new WebSocketEventStreamService(configuration ?? EnabledConfiguration(), eventBus);
    }

    private static WebSocketEventStreamConfiguration EnabledConfiguration()
    {
        return WebSocketEventStreamConfiguration.Default with
        {
            WebSocketEnabled = true,
            ApiTokenReference = Token
        };
    }

    private static WebSocketEventStreamClientRequest Connect(string path)
    {
        return new WebSocketEventStreamClientRequest(path, Token: Token);
    }

    private static IAprsEvent CreateEvent(
        AprsEventType eventType,
        AprsEventCategory category,
        AprsEventSeverity severity = AprsEventSeverity.Info)
    {
        var source = new ExternalSourceMetadata(
            "Simulation",
            ExternalSourceType.Simulation,
            "sim",
            Now,
            ContractDataOrigin.Simulated,
            ExternalTrustLevel.Internal);
        var metadata = AprsEventMetadata.Create(
            eventType,
            category,
            Now,
            source,
            severity,
            relatedCallsign: "N0CALL",
            relatedObjectName: "CHECKPNT1",
            relatedMessageId: "01",
            relatedPacketId: "pkt-1",
            summary: $"{eventType} summary");

        return new AprsEventEnvelope<string>(metadata, eventType == AprsEventType.RawPacketReceived ? "N0CALL>APRS:>Test" : "payload");
    }

    private sealed class FakeClient(string clientId) : IWebSocketEventStreamClient
    {
        public string ClientId { get; } = clientId;
        public bool IsConnected { get; set; } = true;
        public bool ThrowOnSend { get; init; }
        public WebSocketEventStreamClientFilter Filter { get; set; } = WebSocketEventStreamClientFilter.Default;
        public List<WebSocketEventStreamEnvelope> Sent { get; } = [];

        public ValueTask SendAsync(WebSocketEventStreamEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("send failed");
            }

            Sent.Add(envelope);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(string reason, CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTransmitServices
    {
        public int AprsIsTransmitCalls { get; private set; }
        public int RfTransmitCalls { get; private set; }
    }
}
