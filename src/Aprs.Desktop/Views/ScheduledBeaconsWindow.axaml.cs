using Aprs.Desktop.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public partial class ScheduledBeaconsWindow : FloatingPanelWindow
{
    public ScheduledBeaconsWindow() { InitializeComponent(); }

    private void EditButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ScheduledBeaconsViewModel vm)
            vm.IsEditing = true;
    }
}
