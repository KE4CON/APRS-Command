namespace AprsCommand.Api;

public sealed record WebSocketEventStreamHostStatus(
    WebSocketEventStreamState State,
    string BindAddress,
    int Port,
    bool WebSocketEnabled,
    bool LocalhostOnly,
    int ConnectedClientCount,
    string? LastError = null);
