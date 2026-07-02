using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aprs.Desktop.Views;

public partial class MobileCompanionUrlWindow : Window
{
    private readonly string url;

    public MobileCompanionUrlWindow() : this("http://localhost:0/") { }

    public MobileCompanionUrlWindow(string url)
    {
        this.url = url;
        InitializeComponent();
        UrlText.Text = url;
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(url);
            CopyButton.Content = "Copied!";
            await Task.Delay(1500);
            CopyButton.Content = "Copy URL";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
