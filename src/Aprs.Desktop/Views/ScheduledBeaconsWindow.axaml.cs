using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public partial class ScheduledBeaconsWindow : Window
{
    public ScheduledBeaconsWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();

    private void EditButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ScheduledBeaconsViewModel vm)
            vm.IsEditing = true;
    }
}
