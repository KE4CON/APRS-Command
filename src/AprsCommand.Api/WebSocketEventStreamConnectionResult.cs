namespace AprsCommand.Api;

public sealed record WebSocketEventStreamConnectionResult(
    bool Success,
    int StatusCode,
    string? Error = null,
    string? ClientId = null)
{
    public static WebSocketEventStreamConnectionResult Accepted(string clientId)
    {
        return new WebSocketEventStreamConnectionResult(true, 101, ClientId: clientId);
    }

    public static WebSocketEventStreamConnectionResult Rejected(int statusCode, string error)
    {
        return new WebSocketEventStreamConnectionResult(false, statusCode, error);
    }
}
