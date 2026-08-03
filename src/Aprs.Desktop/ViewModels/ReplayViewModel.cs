using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ReplayViewModel : INotifyPropertyChanged
{
    // Cap the wait between packets so a long idle gap in the recording (minutes of silence)
    // doesn't stall playback; and how often the loop re-checks while paused.
    private static readonly TimeSpan MaxInterPacketDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PausePollInterval = TimeSpan.FromMilliseconds(150);
    // During a same-timestamp burst (no gap to wait) the loop would run synchronously and freeze
    // the UI. Yield to the renderer at least this often, and throttle the (expensive) list/progress
    // refresh so a fast burst drains quickly while the window and controls keep painting.
    private static readonly TimeSpan BurstYieldInterval = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan RefreshThrottle = TimeSpan.FromMilliseconds(150);

    private readonly IReplayService replayService;
    private IReplayMapController? mapController;
    private string selectedReplayFilePath = string.Empty;
    private string lastError = string.Empty;
    private ReplaySessionState currentState = ReplaySessionState.Stopped;
    private CancellationTokenSource? playbackCts;
    private bool isStreaming;
    private bool isReplayMode;

    public ReplayViewModel(IReplayService replayService)
    {
        this.replayService = replayService;
        Entries = new ObservableCollection<string>();
        LoadCommand   = new AsyncDesktopCommand(LoadSelectedFileAsync);
        BrowseCommand = new DesktopCommand(async () => await BrowseForLogFileAsync());
        PlayCommand = new AsyncDesktopCommand(PlayAsync);
        PauseCommand = new DesktopCommand(Pause);
        ResumeCommand = new DesktopCommand(Resume);
        StopCommand = new DesktopCommand(Stop);
        ReturnToLiveCommand = new DesktopCommand(ReturnToLive);
        Refresh();
    }

    /// <summary>
    /// Supplies the map controller once the runtime is wired up (avoids a construction-time cycle
    /// between this view model and the live coordinator). Called from application startup.
    /// </summary>
    public void SetMapController(IReplayMapController controller) => mapController = controller;

    /// <summary>True while a replay is actively streaming packets (vs. paused/finished).</summary>
    public bool IsStreaming
    {
        get => isStreaming;
        private set
        {
            if (isStreaming == value)
            {
                return;
            }

            isStreaming = value;
            OnPropertyChanged();
            NotifyStripControls();
        }
    }

    /// <summary>
    /// True from Play until Return to Live — the whole review session, including after playback
    /// finishes. Drives the collapsed control strip and the replay-only map. The full setup UI
    /// shows when this is false.
    /// </summary>
    public bool IsReplayMode
    {
        get => isReplayMode;
        private set
        {
            if (isReplayMode == value)
            {
                return;
            }

            isReplayMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotReplayMode));
            NotifyStripControls();
        }
    }

    /// <summary>Inverse of <see cref="IsReplayMode"/>, for showing the full setup UI.</summary>
    public bool IsNotReplayMode => !isReplayMode;

    // Strip button visibility: Pause/Resume/Stop only while streaming; "Replay again" once a
    // review has stopped or finished but we're still in replay mode (map still showing the log).
    public bool CanPause => isStreaming && !IsPaused;
    public bool CanResume => isStreaming && IsPaused;
    public bool ShowStop => isStreaming;
    public bool ShowReplayAgain => isReplayMode && !isStreaming;

    private void NotifyStripControls()
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(ShowStop));
        OnPropertyChanged(nameof(ShowReplayAgain));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Entries { get; }

    public ICommand LoadCommand { get; }
    public DesktopCommand BrowseCommand { get; }

    public ICommand PlayCommand { get; }

    public DesktopCommand PauseCommand { get; }

    public DesktopCommand ResumeCommand { get; }

    public DesktopCommand StopCommand { get; }

    public DesktopCommand ReturnToLiveCommand { get; }

    public string SelectedReplayFilePath
    {
        get => selectedReplayFilePath;
        set
        {
            if (selectedReplayFilePath == value)
            {
                return;
            }

            selectedReplayFilePath = value;
            OnPropertyChanged();
        }
    }

    public double SpeedMultiplier
    {
        get => replayService.Configuration.SpeedMultiplier;
        set
        {
            replayService.UpdateConfiguration(replayService.Configuration with { SpeedMultiplier = value <= 0 ? 1.0 : value });
            Refresh();
            OnPropertyChanged();
        }
    }

    public bool LoopReplay
    {
        get => replayService.Configuration.LoopReplay;
        set
        {
            replayService.UpdateConfiguration(replayService.Configuration with { LoopReplay = value });
            Refresh();
            OnPropertyChanged();
        }
    }

    public string State { get; private set; } = ReplaySessionState.Stopped.ToString();

    /// <summary>True only while playback is paused. Drives the mutually-exclusive
    /// Pause/Resume buttons so exactly one is shown (they share a layout cell).</summary>
    public bool IsPaused => currentState == ReplaySessionState.Paused;

    public string CurrentPositionText { get; private set; } = "0 / 0";

    public string CurrentTimestampText { get; private set; } = "Unknown";

    public string ProgressText { get; private set; } = "0%";

    public string TransmitStatusText => replayService.Configuration.TransmitDisabled
        ? "Replay transmit disabled"
        : "Replay transmit enabled";

    public string LastError
    {
        get => lastError;
        private set
        {
            if (lastError == value)
            {
                return;
            }

            lastError = value;
            OnPropertyChanged();
        }
    }

    public int TotalPackets => replayService.GetStatus().TotalEntries;

    public async Task LoadSelectedFileAsync()
    {
        try
        {
            await replayService.LoadFromFileAsync(selectedReplayFilePath).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
        }

        Refresh();
    }

    /// <summary>
    /// Streams the loaded log continuously: dispatches each packet, then waits the recorded
    /// gap to the next packet divided by the Speed multiplier (capped, so long silent stretches
    /// don't stall). Pause suspends the stream in place; Stop cancels it; Loop repeats from the top.
    /// </summary>
    public async Task PlayAsync()
    {
        // If the previous run finished, rewind so Play starts fresh instead of no-opping.
        if (currentState == ReplaySessionState.Completed)
        {
            replayService.Stop();
        }

        var timeline = replayService.GetEntries();
        if (timeline.Count == 0)
        {
            Refresh();
            return;
        }

        // Switch the map to a clean, replay-only view. Live keeps ingesting underneath (cached).
        mapController?.EnterReplayMode();
        IsReplayMode = mapController?.IsReplayMode ?? false;

        using var cts = new CancellationTokenSource();
        playbackCts = cts;
        var token = cts.Token;

        IsStreaming = true;
        // Let the strip paint before diving into the (potentially bursty) loop.
        await Task.Delay(1, token).ConfigureAwait(true);

        var lastRefreshStamp = Stopwatch.GetTimestamp();
        var lastYieldStamp = Stopwatch.GetTimestamp();
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (currentState == ReplaySessionState.Paused)
                {
                    await Task.Delay(PausePollInterval, token).ConfigureAwait(true);
                    continue;
                }

                var playedIndex = replayService.GetStatus().CurrentIndex;
                var advanced = await replayService.PlayNextAsync(token).ConfigureAwait(true);

                // Throttle the list/progress refresh — rebuilding it every packet during a fast
                // burst is what made the UI crawl. The map updates on its own coalesced timer.
                if (Stopwatch.GetElapsedTime(lastRefreshStamp) >= RefreshThrottle)
                {
                    Refresh();
                    lastRefreshStamp = Stopwatch.GetTimestamp();
                }

                if (!advanced)
                {
                    // Either paused mid-call (keep waiting) or reached the end with no loop (done).
                    if (currentState == ReplaySessionState.Paused)
                    {
                        continue;
                    }

                    break;
                }

                var delay = ComputeInterPacketDelay(timeline, playedIndex);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token).ConfigureAwait(true);
                    lastYieldStamp = Stopwatch.GetTimestamp();
                }
                else if (Stopwatch.GetElapsedTime(lastYieldStamp) >= BurstYieldInterval)
                {
                    // No gap to wait, but hand the renderer a real slice periodically so the window
                    // and controls keep painting during a long same-timestamp burst.
                    await Task.Delay(1, token).ConfigureAwait(true);
                    lastYieldStamp = Stopwatch.GetTimestamp();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop() cancelled the stream — expected, not an error.
        }
        finally
        {
            playbackCts = null;
            IsStreaming = false;
            Refresh();
        }
    }

    private TimeSpan ComputeInterPacketDelay(IReadOnlyList<ReplayLogEntry> timeline, int playedIndex)
    {
        var nextIndex = playedIndex + 1;
        if (nextIndex < 0 || nextIndex >= timeline.Count)
        {
            return TimeSpan.Zero;
        }

        var gap = timeline[nextIndex].OriginalTimestampUtc - timeline[playedIndex].OriginalTimestampUtc;
        if (gap <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var speed = SpeedMultiplier <= 0 ? 1.0 : SpeedMultiplier;
        var scaled = TimeSpan.FromSeconds(gap.TotalSeconds / speed);
        return scaled > MaxInterPacketDelay ? MaxInterPacketDelay : scaled;
    }

    public void Pause()
    {
        replayService.Pause();
        Refresh();
    }

    public void Resume()
    {
        replayService.Resume();
        Refresh();
    }

    public void Stop()
    {
        playbackCts?.Cancel();
        replayService.Stop();
        Refresh();
    }

    /// <summary>
    /// Ends the replay review and returns the map to live. Stops any playback, then switches the
    /// map back to the live station set (which shows everything that arrived while replaying).
    /// </summary>
    public void ReturnToLive()
    {
        playbackCts?.Cancel();
        replayService.Stop();
        mapController?.ExitReplayMode();
        IsReplayMode = mapController?.IsReplayMode ?? false;
        // Each new review starts at real time; a speed bumped up mid-review doesn't carry over.
        SpeedMultiplier = 1.0;
        Refresh();
    }

    public void Refresh()
    {
        var status = replayService.GetStatus();
        currentState = status.State;
        State = status.State.ToString();
        CurrentPositionText = $"{Math.Min(status.CurrentIndex, status.TotalEntries)} / {status.TotalEntries}";
        CurrentTimestampText = status.CurrentOriginalTimestampUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
        ProgressText = $"{status.ProgressPercent:0}%";
        LastError = status.LastError ?? string.Empty;

        Entries.Clear();
        foreach (var entry in replayService.GetEntries().Take(25))
        {
            Entries.Add($"{entry.OriginalTimestampUtc:HH:mm:ss} {entry.SourceCallsign ?? "Unknown"} {entry.ParsedPacketType ?? "Raw"}");
        }

        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsPaused));
        NotifyStripControls();
        OnPropertyChanged(nameof(CurrentPositionText));
        OnPropertyChanged(nameof(CurrentTimestampText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(TransmitStatusText));
        OnPropertyChanged(nameof(TotalPackets));
        OnPropertyChanged(nameof(SpeedMultiplier));
        OnPropertyChanged(nameof(LoopReplay));
    }

    private async Task BrowseForLogFileAsync()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (topLevel is null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Open APRS packet log",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new Avalonia.Platform.Storage.FilePickerFileType("APRS log files")
                        {
                            Patterns = ["*.aprslog", "*.txt", "*.log", "*.csv", "*.aprs"]
                        },
                        new Avalonia.Platform.Storage.FilePickerFileType("All files")
                        {
                            Patterns = ["*.*"]
                        }
                    ]
                });

            if (files.Count > 0)
            {
                SelectedReplayFilePath = files[0].Path.LocalPath;
                await LoadSelectedFileAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public static ReplayViewModel CreateDesignTime()
    {
        var service = new ReplayService(new NoOpReplayPacketSink());
        service.LoadEntries(
        [
            new RawPacketLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test replay",
                "Position",
                "N0CALL",
                "APRS",
                ["TCPIP*"],
                AprsPacketSource.AprsIs,
                RawPacketLogDirection.Received,
                "aprs-is",
                "APRS-IS",
                RawPacketValidationStatus.Valid,
                [],
                [],
                true,
                null,
                "Replay sample"),
            new RawPacketLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-2),
                "WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132",
                "Weather",
                "WX9XYZ",
                "APRS",
                [],
                AprsPacketSource.Rf,
                RawPacketLogDirection.Received,
                "rf",
                "RF",
                RawPacketValidationStatus.Valid,
                [],
                [],
                true,
                null,
                "Replay weather sample")
        ]);

        return new ReplayViewModel(service);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
