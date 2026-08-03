using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;

namespace Aprs.Desktop.Controls;

/// <summary>
/// A borderless, rounded "floating panel" window. The OS title bar is hidden (the window keeps its
/// native resize frame and system drop shadow), and a custom grip title bar — defined once in the shared
/// control theme — drags the window and closes it. Every feature window derives from this so they all
/// wear the same chrome, and the look can be tuned in one place.
/// </summary>
public class FloatingPanelWindow : Window
{
    /// <summary>Short title shown in the panel's own title bar (distinct from <see cref="Window.Title"/>,
    /// which still drives the taskbar / alt-tab entry).</summary>
    public static readonly StyledProperty<string?> PanelTitleProperty =
        AvaloniaProperty.Register<FloatingPanelWindow, string?>(nameof(PanelTitle));

    public string? PanelTitle
    {
        get => GetValue(PanelTitleProperty);
        set => SetValue(PanelTitleProperty, value);
    }

    public FloatingPanelWindow()
    {
        Background = Avalonia.Media.Brushes.Transparent;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 0;
    }

    // Derived windows (StationListWindow, MessagesWindow, …) resolve THIS type's control theme.
    protected override Type StyleKeyOverride => typeof(FloatingPanelWindow);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Control>("PART_TitleBar") is { } titleBar)
        {
            titleBar.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(args);
                }
            };
        }

        if (e.NameScope.Find<Button>("PART_Close") is { } close)
        {
            close.Click += (_, _) => Close();
        }
    }
}
