namespace AprsCommand.Api;

public sealed record WebSocketEventStreamClientRequest(
    string Path,
    string RemoteAddress = "127.0.0.1",
    string? Token = null,
    WebSocketEventStreamClientFilter? Filter = null);
