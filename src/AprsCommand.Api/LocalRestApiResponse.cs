namespace AprsCommand.Api;

public sealed record LocalRestApiResponse(
    int StatusCode,
    bool Success,
    object? Body = null,
    string? Error = null)
{
    public static LocalRestApiResponse Ok(object? body = null)
    {
        return new LocalRestApiResponse(200, true, body);
    }

    public static LocalRestApiResponse Created(object? body = null)
    {
        return new LocalRestApiResponse(201, true, body);
    }

    public static LocalRestApiResponse ErrorResponse(int statusCode, string error)
    {
        return new LocalRestApiResponse(statusCode, false, Error: error);
    }
}
