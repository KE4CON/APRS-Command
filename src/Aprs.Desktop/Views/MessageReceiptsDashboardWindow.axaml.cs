using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class MessageReceiptsDashboardWindow : Window
{
    public MessageReceiptsDashboardWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
