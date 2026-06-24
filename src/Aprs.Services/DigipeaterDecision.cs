namespace Aprs.Services;

public enum DigipeaterDecision
{
    Allowed,
    Blocked,
    Duplicate,
    RateLimited,
    Invalid,
    TransmitDisabled,
    NoMatchingAlias,
    Error
}
