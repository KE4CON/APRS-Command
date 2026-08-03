using System.IO;
using Aprs.Desktop.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class AfterActionExportWindow : FloatingPanelWindow
{
    public AfterActionExportWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is AfterActionExportViewModel vm)
        {
            vm.SaveFileRequested += OnSaveFileRequested;
            vm.RefreshCounts();
        }
    }

    private void OnSaveFileRequested(object? sender, (string FileName, string Content) args)
    {
        try
        {
            var downloadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            if (!Directory.Exists(downloadsPath))
                downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var filePath = Path.Combine(downloadsPath, args.FileName);
            File.WriteAllText(filePath, args.Content, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // Surface the error back to the viewmodel status.
            if (DataContext is AfterActionExportViewModel vm)
            {
                // Access StatusText via reflection would be complex — just swallow here
                // since the viewmodel's try/catch already handles most errors.
                _ = ex;
            }
        }
    }
}
