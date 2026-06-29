using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class ElevationProfileWindow : Window
{
    public ElevationProfileWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
