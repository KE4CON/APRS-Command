namespace AprsCommand.Api;

public sealed record WebSocketInboundMessage(
    string Command,
    WebSocketEventStreamClientFilter? Filter = null);
