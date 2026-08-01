using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Guards the menu keyboard-gesture strings: XAML compiles even when a gesture is unparseable, but
/// <c>KeyGesture.Parse</c> runs at runtime (when the window is built) and throws. "Ctrl+/" crashed the
/// app on first launch; "Ctrl+OemQuestion" is the parseable form for the '/' key.
/// </summary>
public sealed class KeyboardGestureTests
{
    [Fact]
    public void ShortcutsMenuGesture_Parses()
    {
        var gesture = Avalonia.Input.KeyGesture.Parse("Ctrl+OemQuestion");

        Assert.Equal(Avalonia.Input.Key.OemQuestion, gesture.Key);
        Assert.Equal(Avalonia.Input.KeyModifiers.Control, gesture.KeyModifiers);
    }

    [Fact]
    public void RawSlashGesture_IsNotParseable()
    {
        // Documents why we don't use "Ctrl+/" directly.
        Assert.ThrowsAny<System.Exception>(() => Avalonia.Input.KeyGesture.Parse("Ctrl+/"));
    }
}
