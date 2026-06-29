using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class ShadowBeaconWindow : Window
{
    public ShadowBeaconWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
