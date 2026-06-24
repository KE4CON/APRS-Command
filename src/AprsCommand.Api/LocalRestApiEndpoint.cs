namespace AprsCommand.Api;

public sealed record LocalRestApiEndpoint(
    string Method,
    string Path,
    bool RequiresWritePermission = false,
    bool RequiresTransmitPermission = false);
