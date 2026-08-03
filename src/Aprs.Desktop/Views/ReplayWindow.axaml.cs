using System;
using System.ComponentModel;
using Aprs.Desktop.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class ReplayWindow : FloatingPanelWindow
{
    private ReplayViewModel? replay;
    private bool closed;

    public ReplayWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();

        if (DataContext is MainWindowViewModel main)
        {
            replay = main.Replay;
            replay.PropertyChanged += OnReplayPropertyChanged;
            // Don't touch visibility here — the window is shown normally by ShowWithState. Only
            // react to later IsReplayMode changes.
        }
    }

    private void OnReplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReplayViewModel.IsReplayMode) && replay is not null)
        {
            ApplyReplayVisibility(replay.IsReplayMode);
        }
    }

    // During a replay review the controls live on an on-map bar, so this window gets out of the
    // way and returns when the review ends. Guard against a window the user already closed — its
    // subscription can still fire, and Show() on a closed window throws.
    private void ApplyReplayVisibility(bool inReplay)
    {
        if (closed)
        {
            return;
        }

        if (inReplay)
        {
            Hide();
        }
        else if (!IsVisible)
        {
            Show();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (replay is not null)
        {
            replay.PropertyChanged -= OnReplayPropertyChanged;
            replay = null;
        }
    }
}
