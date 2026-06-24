namespace Aprs.Services;

public enum AprsMessageDeliveryState
{
    Pending,
    Queued,
    Sent,
    WaitingForAck,
    Acknowledged,
    Rejected,
    RetryPending,
    Failed,
    Cancelled
}
