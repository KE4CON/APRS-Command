using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class NetControlView : Window
{
    public NetControlView() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
