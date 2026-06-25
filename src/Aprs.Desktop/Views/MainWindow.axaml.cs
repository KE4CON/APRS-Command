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
            subscribedViewModel.SettingsRequested -= OnSettingsRequested;
        }

        subscribedViewModel = DataContext as MainWindowViewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.HelpRequested += OnHelpRequested;
            subscribedViewModel.SettingsRequested += OnSettingsRequested;
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

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // The tabbed SettingsView binds its tabs to properties on the main view model
        // (Connections, FirstRunSetup, etc.), so the window shares that data context.
        var settingsWindow = new SettingsWindow
        {
            DataContext = subscribedViewModel
        };

        settingsWindow.Show(this);
    }
}
