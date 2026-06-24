namespace AprsCommand.Api;

public sealed record WebSocketInboundMessageResult(
    bool Success,
    string? ResponseType = null,
    string? Error = null)
{
    public static WebSocketInboundMessageResult Accepted(string responseType)
    {
        return new WebSocketInboundMessageResult(true, responseType);
    }

    public static WebSocketInboundMessageResult Rejected(string error)
    {
        return new WebSocketInboundMessageResult(false, Error: error);
    }
}
