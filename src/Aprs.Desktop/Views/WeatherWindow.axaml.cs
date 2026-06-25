using Avalonia.Controls;
using Avalonia.Interactivity;
namespace Aprs.Desktop.Views;
public sealed partial class WeatherWindow : Window
{
    public WeatherWindow() { InitializeComponent(); }
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
