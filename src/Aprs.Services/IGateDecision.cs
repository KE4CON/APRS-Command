namespace Aprs.Services;

public enum IGateDecision
{
    Allowed,
    Blocked,
    Duplicate,
    RateLimited,
    Invalid,
    TransmitDisabled,
    AprsIsDisconnected,
    Error
}
