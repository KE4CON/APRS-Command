using Avalonia.Controls;
using Avalonia.Interactivity;
namespace Aprs.Desktop.Views;
public sealed partial class MessagesWindow : Window
{
    public MessagesWindow() { InitializeComponent(); }
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
