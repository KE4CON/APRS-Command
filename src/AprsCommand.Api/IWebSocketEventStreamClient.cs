namespace AprsCommand.Api;

public interface IWebSocketEventStreamClient
{
    string ClientId { get; }
    bool IsConnected { get; }
    WebSocketEventStreamClientFilter Filter { get; set; }

    ValueTask SendAsync(WebSocketEventStreamEnvelope envelope, CancellationToken cancellationToken = default);

    ValueTask DisconnectAsync(string reason, CancellationToken cancellationToken = default);
}
