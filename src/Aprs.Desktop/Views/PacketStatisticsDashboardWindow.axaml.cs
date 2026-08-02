using System;
using Aprs.Desktop.Controls;
using Avalonia.Interactivity;

namespace Aprs.Desktop.Views;

public partial class PacketStatisticsDashboardWindow : FloatingPanelWindow
{
    public PacketStatisticsDashboardWindow()
    {
        InitializeComponent();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.PacketStatisticsDashboardViewModel vm)
            vm.ResetStats();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ViewModels.PacketStatisticsDashboardViewModel vm)
            vm.Dispose();
        base.OnClosed(e);
    }
}
