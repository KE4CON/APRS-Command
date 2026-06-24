namespace Aprs.Services;

public enum IGateCandidateState
{
    Unknown,
    Candidate,
    Rejected,
    Duplicate,
    AlreadySeenOnAprsIs,
    Invalid,
    Expired
}
