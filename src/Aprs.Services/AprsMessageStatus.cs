namespace Aprs.Services;

public enum AprsMessageStatus
{
    Pending,
    Received,
    Draft,
    Queued,
    Sent,
    WaitingForAck,
    RetryPending,
    Failed,
    Acknowledged,
    Rejected,
    Cancelled
}
