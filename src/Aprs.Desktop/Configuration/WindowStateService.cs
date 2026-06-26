using Avalonia.Controls;

namespace Aprs.Desktop.Configuration;

/// <summary>
/// Saves and restores window positions and sizes. Call <see cref="Restore"/> immediately after
/// constructing a window, and <see cref="Save"/> from the window's Closing event.
/// </summary>
public static class WindowStateService
{
    private const double MinWidth  = 300;
    private const double MinHeight = 200;

    /// <summary>
    /// Restores a saved position/size to <paramref name="window"/> if one exists.
    /// Uses the window's type name as the key.
    /// </summary>
    public static void Restore(Window window, IAppSettingsStore store)
    {
        try
        {
            var key    = window.GetType().Name;
            var saved  = store.Load().Windows.Get(key);
            if (saved is null) return;

            // Validate — don't restore off-screen or impossibly small.
            if (saved.Width  < MinWidth  || saved.Height < MinHeight) return;

            var screens = window.Screens;
            if (screens is null) return;

            // Check at least part of the window would be on a screen.
            bool onScreen = screens.All.Any(s =>
            {
                var b = s.WorkingArea;
                return saved.X < (b.X + b.Width)  / s.Scaling
                    && saved.Y < (b.Y + b.Height) / s.Scaling
                    && saved.X + saved.Width  > b.X / s.Scaling
                    && saved.Y + saved.Height > b.Y / s.Scaling;
            });

            if (!onScreen) return;

            window.Position = new Avalonia.PixelPoint((int)saved.X, (int)saved.Y);
            window.Width    = saved.Width;
            window.Height   = saved.Height;
        }
        catch
        {
            // Never crash on state restore — just use defaults.
        }
    }

    /// <summary>
    /// Saves the current position/size of <paramref name="window"/> to the settings store.
    /// </summary>
    public static void Save(Window window, IAppSettingsStore store)
    {
        try
        {
            var key    = window.GetType().Name;
            var record = new WindowStateRecord(
                X:      window.Position.X,
                Y:      window.Position.Y,
                Width:  window.Width,
                Height: window.Height);

            store.Update(s => s with { Windows = s.Windows.With(key, record) });
        }
        catch { }
    }
}
