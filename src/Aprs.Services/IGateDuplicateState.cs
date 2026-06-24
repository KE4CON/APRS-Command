namespace Aprs.Services;

public enum IGateDuplicateState
{
    Unknown,
    NotSeen,
    SeenOnAprsIs,
    DuplicateWithinWindow,
    Expired
}
