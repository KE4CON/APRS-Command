using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace Aprs.Desktop.Views;
public sealed partial class RawPacketsWindow : Window
{
    public RawPacketsWindow() { InitializeComponent(); }
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
    private async void SaveLog_Click(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save packet log",
            DefaultExtension = "aprslog",
            SuggestedFileName = $"aprs-log-{DateTime.Now:yyyyMMdd-HHmm}",
            FileTypeChoices = [new FilePickerFileType("APRS packet log") { Patterns = ["*.aprslog", "*.txt"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new System.IO.StreamWriter(stream);
        await writer.WriteLineAsync($"# APRS Command packet log — {DateTimeOffset.Now:u}");
        await writer.WriteLineAsync("# Full packet export will be enabled in a future update.");
    }
}
