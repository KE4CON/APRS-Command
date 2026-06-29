using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class CoveragePredictionWindow : Window
{
    public CoveragePredictionWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
    }

    private void CopyPhgButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.CoveragePredictionViewModel vm &&
            !string.IsNullOrWhiteSpace(vm.CalcPhgString))
        {
            _ = Clipboard?.SetTextAsync(vm.CalcPhgString);
        }
    }
}
