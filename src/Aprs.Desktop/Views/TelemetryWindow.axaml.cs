using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public partial class TelemetryWindow : Window
{
    public TelemetryWindow()
    {
        InitializeComponent();
        DataContext = TelemetryViewModel.CreateDesignTime();
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}
