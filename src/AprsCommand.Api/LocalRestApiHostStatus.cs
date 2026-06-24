namespace AprsCommand.Api;

public sealed record LocalRestApiHostStatus(
    LocalRestApiState State,
    string BindAddress,
    int Port,
    bool ApiEnabled,
    bool LocalhostOnly,
    string? LastError = null);
