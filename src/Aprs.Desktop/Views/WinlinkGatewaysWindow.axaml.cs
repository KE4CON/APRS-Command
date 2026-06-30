using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class WinlinkGatewaysWindow : Window
{
    public WinlinkGatewaysWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
