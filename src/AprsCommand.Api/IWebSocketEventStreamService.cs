using Aprs.Services;

namespace AprsCommand.Api;

public interface IWebSocketEventStreamService
{
    WebSocketEventStreamHostStatus Status { get; }
    IReadOnlyList<WebSocketEventStreamEndpoint> Endpoints { get; }
    IReadOnlyList<IWebSocketEventStreamClient> ConnectedClients { get; }

    Task<WebSocketEventStreamHostStatus> StartAsync(CancellationToken cancellationToken = default);
    Task<WebSocketEventStreamHostStatus> StopAsync(CancellationToken cancellationToken = default);
    Task<WebSocketEventStreamConnectionResult> ConnectClientAsync(
        WebSocketEventStreamClientRequest request,
        IWebSocketEventStreamClient client,
        CancellationToken cancellationToken = default);
    Task DisconnectClientAsync(string clientId, string reason, CancellationToken cancellationToken = default);
    Task<WebSocketInboundMessageResult> HandleInboundMessageAsync(
        string clientId,
        WebSocketInboundMessage message,
        CancellationToken cancellationToken = default);
    WebSocketEventStreamEnvelope ToEnvelope(IAprsEvent aprsEvent, string streamName = "events");
    Task<int> BroadcastAsync(IAprsEvent aprsEvent, CancellationToken cancellationToken = default);
}
