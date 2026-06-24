using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.HelpRequested -= OnHelpRequested;
        }

        subscribedViewModel = DataContext as MainWindowViewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.HelpRequested += OnHelpRequested;
        }
    }

    private void OnHelpRequested(object? sender, EventArgs e)
    {
        var helpWindow = new HelpWindow
        {
            DataContext = HelpViewModel.CreateDefault()
        };

        helpWindow.Show(this);
    }
}
