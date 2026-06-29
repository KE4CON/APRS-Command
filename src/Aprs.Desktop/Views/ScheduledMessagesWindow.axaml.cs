using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class ScheduledMessagesWindow : Window
{
    public ScheduledMessagesWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
