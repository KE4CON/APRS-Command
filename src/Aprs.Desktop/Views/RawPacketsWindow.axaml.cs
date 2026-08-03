using System;
using Aprs.Desktop.Controls;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Aprs.Desktop.Views;

public sealed partial class RawPacketsWindow : FloatingPanelWindow
{
    public RawPacketsWindow() { InitializeComponent(); }

    private async void SaveLog_Click(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("SaveStatusText");

        var log = (DataContext as MainWindowViewModel)?.RawPacketLog;
        if (log is null)
        {
            return;
        }

        var entries = log.GetEntriesForExport();
        if (entries.Count == 0)
        {
            if (status is not null)
            {
                status.Text = "No packets captured yet — connect a source and let packets arrive first.";
            }

            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save packet log",
            DefaultExtension = "aprslog",
            SuggestedFileName = $"aprs-log-{DateTime.Now:yyyyMMdd-HHmm}",
            FileTypeChoices = [new FilePickerFileType("APRS packet log") { Patterns = ["*.aprslog", "*.csv", "*.txt"] }]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            foreach (var line in RawPacketLogCsvExporter.ToCsvLines(entries))
            {
                await writer.WriteLineAsync(line);
            }

            if (status is not null)
            {
                status.Text = $"Saved {entries.Count} packet{(entries.Count == 1 ? string.Empty : "s")} to {file.Name} — load it in the Replay window.";
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            if (status is not null)
            {
                status.Text = $"Could not save log: {ex.Message}";
            }
        }
    }
}
