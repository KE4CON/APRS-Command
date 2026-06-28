using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class OfflineMapDownloadWindow : Window
{
    public OfflineMapDownloadWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
    }
}
