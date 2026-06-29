using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class MessageBroadcastWindow : Window
{
    public MessageBroadcastWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
