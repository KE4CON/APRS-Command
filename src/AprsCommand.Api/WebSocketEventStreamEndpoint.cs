namespace AprsCommand.Api;

public sealed record WebSocketEventStreamEndpoint(
    string Path,
    string StreamName,
    string Description,
    bool Implemented = true);
