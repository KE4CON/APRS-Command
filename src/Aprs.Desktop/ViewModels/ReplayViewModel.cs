using System.Collections.ObjectModel;
using System.ComponentModel;
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

    private readonly IReplayService replayService;
    private string selectedReplayFilePath = string.Empty;
    private string lastError = string.Empty;
    private ReplaySessionState currentState = ReplaySessionState.Stopped;
    private CancellationTokenSource? playbackCts;

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
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Entries { get; }

    public ICommand LoadCommand { get; }
    public DesktopCommand BrowseCommand { get; }

    public ICommand PlayCommand { get; }

    public DesktopCommand PauseCommand { get; }

    public DesktopCommand ResumeCommand { get; }

    public DesktopCommand StopCommand { get; }

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

        using var cts = new CancellationTokenSource();
        playbackCts = cts;
        var token = cts.Token;

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
                Refresh();

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
