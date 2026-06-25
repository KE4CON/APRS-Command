using Avalonia.Controls;
using Avalonia.Interactivity;
namespace Aprs.Desktop.Views;
public sealed partial class ObjectsWindow : Window
{
    public ObjectsWindow() { InitializeComponent(); }
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
