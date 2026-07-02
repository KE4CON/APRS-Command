using Avalonia.Controls;

namespace Aprs.Desktop.Views;

public partial class RepeaterDirectoryWindow : Window
{
    public RepeaterDirectoryWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ViewModels.RepeaterDirectoryViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
