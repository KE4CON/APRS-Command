using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class StationSetupView : UserControl
{
    private bool phgLoading;

    public StationSetupView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPhgDropdownsFromViewModel();
    }

    /// <summary>
    /// When any PHG dropdown changes, rebuild the PHG string in the viewmodel.
    /// </summary>
    private void PhgDropdown_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (phgLoading) return;
        if (DataContext is not StationSetupViewModel vm) return;

        vm.ApplyPhgFromDropdowns(
            PhgPowerCombo.SelectedIndex,
            PhgHeightCombo.SelectedIndex,
            PhgGainCombo.SelectedIndex,
            PhgDirectivityCombo.SelectedIndex);

        PhgResultText.Text = string.IsNullOrEmpty(vm.PhgData)
            ? "PHG not set — will be omitted from beacons."
            : $"Beacon will include: {vm.PhgData}";
    }

    /// <summary>
    /// When the DataContext changes (e.g. settings loaded or reverted), sync the
    /// dropdowns back from the viewmodel's current PHG string.
    /// </summary>
    private void SyncPhgDropdownsFromViewModel()
    {
        if (DataContext is not StationSetupViewModel vm) return;

        phgLoading = true;
        try
        {
            var (p, h, g, d) = vm.ParsePhgToDropdownIndices();
            PhgPowerCombo.SelectedIndex       = p;
            PhgHeightCombo.SelectedIndex      = h;
            PhgGainCombo.SelectedIndex        = g;
            PhgDirectivityCombo.SelectedIndex = d;

            PhgResultText.Text = string.IsNullOrEmpty(vm.PhgData)
                ? "PHG not set — will be omitted from beacons."
                : $"Beacon will include: {vm.PhgData}";
        }
        finally
        {
            phgLoading = false;
        }
    }
}
