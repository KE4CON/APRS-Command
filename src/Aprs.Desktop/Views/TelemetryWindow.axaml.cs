using Aprs.Desktop.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public partial class TelemetryWindow : FloatingPanelWindow
{
    public TelemetryWindow()
    {
        InitializeComponent();
        DataContext = TelemetryViewModel.CreateDesignTime();
    }
}
