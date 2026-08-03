namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Lets the Replay view model switch the live map between "live" and "replay review" modes
/// without taking a hard dependency on the runtime coordinator (which is wired up after the
/// view models exist). Implemented by <c>LiveDataCoordinator</c>.
/// </summary>
public interface IReplayMapController
{
    /// <summary>True while the map is showing the replay-only station set.</summary>
    bool IsReplayMode { get; }

    /// <summary>
    /// Switches the map to a clean, replay-only view. The live station database keeps
    /// ingesting in the background (cached) but is not shown until <see cref="ExitReplayMode"/>.
    /// </summary>
    void EnterReplayMode();

    /// <summary>
    /// Returns the map to live, showing current live stations plus everything that arrived
    /// (was cached) while the replay was playing. The replay station set is discarded.
    /// </summary>
    void ExitReplayMode();
}
