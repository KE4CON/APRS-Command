namespace Aprs.Services;

public sealed record AprsEventPublishResult(
    IAprsEvent Event,
    int SubscriberCount,
    IReadOnlyList<AprsEventHandlerResult> HandlerResults)
{
    public bool Success => HandlerResults.All(result => result.Success);
}
