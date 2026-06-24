using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed record LocalRestApiRequest(
    string Method,
    string Path,
    object? Body = null,
    string? Token = null,
    IReadOnlyList<ExtensionPermission>? Permissions = null,
    string RemoteAddress = "127.0.0.1");
