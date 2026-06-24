using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ReplayViewModel : INotifyPropertyChanged
{
    private readonly IReplayService replayService;
    private string selectedReplayFilePath = string.Empty;
    private string lastError = string.Empty;

    public ReplayViewModel(IReplayService replayService)
    {
        this.replayService = replayService;
        Entries = new ObservableCollection<string>();
        LoadCommand = new DesktopCommand(LoadSelectedFile);
        PlayCommand = new DesktopCommand(Play);
        PauseCommand = new DesktopCommand(Pause);
        ResumeCommand = new DesktopCommand(Resume);
        StopCommand = new DesktopCommand(Stop);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Entries { get; }

    public DesktopCommand LoadCommand { get; }

    public DesktopCommand PlayCommand { get; }

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

    public void LoadSelectedFile()
    {
        try
        {
            replayService.LoadFromFileAsync(selectedReplayFilePath).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
        }

        Refresh();
    }

    public void Play()
    {
        replayService.PlayNextAsync().GetAwaiter().GetResult();
        Refresh();
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
        replayService.Stop();
        Refresh();
    }

    public void Refresh()
    {
        var status = replayService.GetStatus();
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
        OnPropertyChanged(nameof(CurrentPositionText));
        OnPropertyChanged(nameof(CurrentTimestampText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(TransmitStatusText));
        OnPropertyChanged(nameof(TotalPackets));
        OnPropertyChanged(nameof(SpeedMultiplier));
        OnPropertyChanged(nameof(LoopReplay));
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
