namespace Aprs.Services;

public enum ReplaySessionState
{
    Stopped,
    Loading,
    Ready,
    Playing,
    Paused,
    Completed,
    Faulted
}
