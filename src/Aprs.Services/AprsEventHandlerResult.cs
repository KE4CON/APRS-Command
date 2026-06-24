namespace Aprs.Services;

public sealed record AprsEventHandlerResult(
    bool Success,
    string? ErrorMessage = null,
    Exception? Exception = null)
{
    public static AprsEventHandlerResult Handled { get; } = new(true);

    public static AprsEventHandlerResult Failed(string errorMessage, Exception? exception = null)
    {
        return new AprsEventHandlerResult(false, errorMessage, exception);
    }
}
