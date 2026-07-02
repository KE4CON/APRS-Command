using Avalonia.Controls;

namespace Aprs.Desktop.Views;

public partial class KeyboardShortcutsWindow : Window
{
    public KeyboardShortcutsWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}
